-- Deploys dbo.CleanEnvironment: a full data reset for the ERP database, for reusing an
-- environment (demo, staging, a stuck integration run) without rebuilding it from migrations.
--
-- Every table is emptied except:
--   * __EFMigrationsHistory       - EF's own bookkeeping, never data.
--   * VatRate                     - seeded once by migration 20260915135953_AddVatRates and
--                                    deliberately never reseeded on startup (see that migration's
--                                    comment); a wipe cannot get these rows back without a DBA
--                                    re-running InsertData by hand.
--   * SubscriptionPlan            - the plan catalog. It carries no CompanyId, so it is reference
--                                    data the same way VatRate is, not a company's operational data.
--
-- Everything else (companies, products, documents, stock, series counters, ...) is operational
-- data, safe to lose in a clean.
--
-- Deploy once per environment:
--   sqlcmd -S <server> -d <database> -i clean-erp-environment.sql
--
-- Run whenever the environment needs wiping. @Confirm exists so a stray EXEC without arguments
-- fails loudly instead of emptying a database someone meant to just query:
--   sqlcmd -S <server> -d <database> -Q "EXEC dbo.CleanEnvironment @Confirm = N'WIPE-ERP';"

CREATE OR ALTER PROCEDURE dbo.CleanEnvironment
    @Confirm NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Confirm <> N'WIPE-ERP'
    BEGIN
        RAISERROR('Pass @Confirm = N''WIPE-ERP'' to run this. It empties every operational table in %s.', 16, 1, DB_NAME());
        RETURN;
    END

    DECLARE @excluded TABLE (TableName SYSNAME);
    INSERT INTO @excluded (TableName) VALUES (N'__EFMigrationsHistory'), (N'VatRate'), (N'SubscriptionPlan');

    BEGIN TRANSACTION;

    BEGIN TRY
        DECLARE @sql NVARCHAR(MAX) = N'';

        -- Constraints off for the duration, so tables need not be emptied in dependency order - an
        -- order that would otherwise have to be maintained by hand every time a foreign key is
        -- added. Built from sys.tables rather than sp_MSforeachtable: that procedure does not exist
        -- on Azure SQL Database, which is where this also needs to run.
        SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name)
            + N' NOCHECK CONSTRAINT ALL;' + CHAR(13)
        FROM sys.tables
        WHERE is_ms_shipped = 0;

        SELECT @sql = @sql + N'DELETE FROM ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' + CHAR(13)
        FROM sys.tables
        WHERE is_ms_shipped = 0
          AND name NOT IN (SELECT TableName FROM @excluded);

        -- Identity columns are reseeded so the emptied tables start again from the same values a
        -- freshly migrated database would have; DELETE alone leaves the counter where it was.
        SELECT @sql = @sql
            + N'DBCC CHECKIDENT (''' + SCHEMA_NAME(t.schema_id) + N'.' + t.name + N''', RESEED, 0);' + CHAR(13)
        FROM sys.tables t
        JOIN sys.identity_columns ic ON ic.object_id = t.object_id
        WHERE t.is_ms_shipped = 0
          AND t.name NOT IN (SELECT TableName FROM @excluded);

        SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name)
            + N' WITH CHECK CHECK CONSTRAINT ALL;' + CHAR(13)
        FROM sys.tables
        WHERE is_ms_shipped = 0;

        EXEC sp_executesql @sql;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    PRINT 'ERP environment cleaned. VatRate and SubscriptionPlan were left untouched.';
END
GO

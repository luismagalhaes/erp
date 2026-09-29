-- Deploys dbo.CleanEnvironment: resets the user side of the Identity database for reusing an
-- environment (demo, staging, a stuck integration run), without touching IdentityServer's
-- configuration.
--
-- Only these tables are emptied, all of them user/session data with nothing seeded about them:
--   ApplicationDbContext : AspNetUsers, AspNetRoles, AspNetRoleClaims, AspNetUserClaims,
--                           AspNetUserLogins, AspNetUserRoles, AspNetUserTokens, LoginAudits,
--                           SignUpAttempts, OnboardingRequests
--   PersistedGrantDbContext : PersistedGrants, DeviceCodes, PushedAuthorizationRequests,
--                           ServerSideSessions - tokens/sessions issued to the users being wiped,
--                           so they would be orphaned (and unusable) if left behind
--
-- A closed whitelist rather than "everything except clients": that way a table added later starts
-- out protected by default instead of wiped by default, and nothing here needs to guess at the
-- exact set of Duende config tables (Clients + its Client* children, ApiResources, ApiScopes,
-- IdentityResources, IdentityProviders, ...). None of those are touched, nor is dbo.Keys (the
-- token signing key material - not user data, and rotating it would invalidate every other
-- environment's tokens too, which is well outside "clean the users").
--
-- Deploy once per environment:
--   sqlcmd -S <server> -d <database> -i clean-identity-environment.sql
--
-- Run whenever the environment needs wiping. @Confirm exists so a stray EXEC without arguments
-- fails loudly instead of emptying a database someone meant to just query:
--   sqlcmd -S <server> -d <database> -Q "EXEC dbo.CleanEnvironment @Confirm = N'WIPE-IDENTITY-USERS';"
--
-- Afterwards, restart Erp.Identity: SeedData.InitializeAsync (see
-- src/Identity/Erp.Identity.Storage/Data/SeedData.cs) recreates the baseline AspNetRoles rows and
-- the SuperAdmin AspNetUsers row from AdminUser:Email/AdminUser:Password on the next startup, the
-- same way it does on a brand new database.

CREATE OR ALTER PROCEDURE dbo.CleanEnvironment
    @Confirm NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Confirm <> N'WIPE-IDENTITY-USERS'
    BEGIN
        RAISERROR('Pass @Confirm = N''WIPE-IDENTITY-USERS'' to run this. It empties every user/session table in %s. Clients, scopes and resources are left alone.', 16, 1, DB_NAME());
        RETURN;
    END

    DECLARE @included TABLE (TableName SYSNAME);
    INSERT INTO @included (TableName) VALUES
        (N'AspNetUsers'), (N'AspNetRoles'), (N'AspNetRoleClaims'), (N'AspNetUserClaims'),
        (N'AspNetUserLogins'), (N'AspNetUserRoles'), (N'AspNetUserTokens'),
        (N'LoginAudits'), (N'SignUpAttempts'), (N'OnboardingRequests'),
        (N'PersistedGrants'), (N'DeviceCodes'), (N'PushedAuthorizationRequests'), (N'ServerSideSessions');

    BEGIN TRANSACTION;

    BEGIN TRY
        DECLARE @sql NVARCHAR(MAX) = N'';

        -- Constraints off for the duration, so the tables need not be emptied in dependency order
        -- (AspNetUserClaims/Logins/Tokens/Roles all reference AspNetUsers, AspNetRoleClaims
        -- references AspNetRoles) - an order that would otherwise have to be maintained by hand.
        -- Scoped to only the whitelisted tables: nothing outside it is touched, not even to
        -- disable and re-enable its constraints.
        SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name)
            + N' NOCHECK CONSTRAINT ALL;' + CHAR(13)
        FROM sys.tables t
        JOIN @included i ON i.TableName = t.name
        WHERE t.is_ms_shipped = 0;

        SELECT @sql = @sql + N'DELETE FROM ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) + N';' + CHAR(13)
        FROM sys.tables t
        JOIN @included i ON i.TableName = t.name
        WHERE t.is_ms_shipped = 0;

        -- Identity columns are reseeded so the emptied tables start again from the same values a
        -- freshly migrated database would have; DELETE alone leaves the counter where it was.
        SELECT @sql = @sql
            + N'DBCC CHECKIDENT (''' + SCHEMA_NAME(t.schema_id) + N'.' + t.name + N''', RESEED, 0);' + CHAR(13)
        FROM sys.tables t
        JOIN @included i ON i.TableName = t.name
        JOIN sys.identity_columns ic ON ic.object_id = t.object_id
        WHERE t.is_ms_shipped = 0;

        SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name)
            + N' WITH CHECK CHECK CONSTRAINT ALL;' + CHAR(13)
        FROM sys.tables t
        JOIN @included i ON i.TableName = t.name
        WHERE t.is_ms_shipped = 0;

        EXEC sp_executesql @sql;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    PRINT 'Identity users cleaned. Clients, scopes and resources were left untouched. Restart Erp.Identity to reseed roles and the SuperAdmin user.';
END
GO

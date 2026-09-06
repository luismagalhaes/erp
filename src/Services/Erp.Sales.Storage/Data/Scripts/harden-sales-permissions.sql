-- Makes the immutability of issued documents a database guarantee instead of an application
-- promise, as required for certification (Portaria 363/2010).
--
-- Run once per environment, after the migrations, replacing the principal below with the login
-- or role the application connects as. Do NOT grant the application db_owner.
--
--   sqlcmd -S <server> -d ErpPortugal_Sales -i harden-sales-permissions.sql -v principal="erp_sales_app"

DECLARE @principal SYSNAME = N'$(principal)';
DECLARE @sql NVARCHAR(MAX) = N'';

-- Documents, lines, tax totals, status changes and events are append-only.
DECLARE @appendOnlyTables TABLE (name SYSNAME);
INSERT INTO @appendOnlyTables (name)
VALUES (N'SalesDocument'), (N'SalesDocumentLine'), (N'DocumentTaxSummary'), (N'DocumentStatusChange');

SELECT @sql = @sql
    + N'GRANT SELECT, INSERT ON [sales].[' + name + N'] TO [' + @principal + N'];' + CHAR(13)
    + N'DENY UPDATE, DELETE ON [sales].[' + name + N'] TO [' + @principal + N'];' + CHAR(13)
FROM @appendOnlyTables;

-- The series is the one table that legitimately needs UPDATE: it carries the sequence counter
-- advanced inside the issuing transaction, and the validation code returned by the tax authority.
SET @sql = @sql
    + N'GRANT SELECT, INSERT, UPDATE ON [sales].[Series] TO [' + @principal + N'];' + CHAR(13)
    + N'DENY DELETE ON [sales].[Series] TO [' + @principal + N'];' + CHAR(13);

EXEC sp_executesql @sql;

PRINT 'Sales permissions hardened for principal ' + @principal + '.';

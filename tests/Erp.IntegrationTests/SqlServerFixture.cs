using Erp.Api.Services;
using Erp.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Erp.IntegrationTests;

/// <summary>
/// The database these tests run against, and the container they run in.
/// </summary>
/// <remarks>
/// It uses a database you create once and keep — see the README next to this file — rather than
/// making one per run. Nothing is dropped afterwards: a test database that survives is one you can
/// open and look at when something fails, which is most of the value of having one.
/// <para>
/// The schema is brought up to date by <b>running the migrations</b>, not by <c>EnsureCreated</c>.
/// That way every run also checks that the migration history applies, which is the one thing no
/// unit test can tell us and the thing that breaks quietly.
/// </para>
/// <para>
/// The container is built with <see cref="Modules.AddModules"/> — the same call
/// <c>Program.cs</c> makes — so what these tests exercise is the wiring the API actually uses.
/// </para>
/// </remarks>
public sealed class SqlServerFixture : IAsyncLifetime
{
    /// <summary>Overrides the whole connection string, for a server that is not the local default.</summary>
    private const string ConnectionVariable = "ERP_TEST_SQL";

    /// <summary>The database to create. Named in one place so the README and the error agree.</summary>
    public const string DatabaseName = "ErpIntegrationTests";

    private const string DefaultConnection =
        $@"Server=(localdb)\MSSQLLocalDB;Database={DatabaseName};Integrated Security=true;TrustServerCertificate=true;";

    private ServiceProvider? _provider;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        ConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable) ?? DefaultConnection;

        await EnsureReachableAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ErpDb"] = ConnectionString,
                // Signing needs a key. Generating a throwaway one is what development does, and a
                // test has even less business carrying a real certificate.
                ["Fiscal:IssuerTaxId"] = "500123456",
                ["Fiscal:CertificateNumber"] = "9999"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddModules(configuration, allowDevelopmentKeyGeneration: true);

        // validateScopes catches a scoped service captured by a singleton, which is the kind of
        // wiring mistake that only shows up under load in production.
        _provider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await context.Database.MigrateAsync();
        await ClearAsync(context);
    }

    /// <summary>
    /// Empties every table before the run, leaving the migration history alone.
    /// </summary>
    /// <remarks>
    /// The database is kept between runs, so without this it would grow forever and — worse — the
    /// companies one run left behind would collide with the tax ids the next one generates. Clearing
    /// at the <b>start</b> rather than at the end is deliberate: what a failing run leaves behind is
    /// still there to be looked at.
    /// </remarks>
    private static async Task ClearAsync(AppDbContext context)
    {
        // Constraints off for the duration, so the tables need not be emptied in dependency order —
        // an order that would have to be maintained by hand every time a foreign key is added.
        await context.Database.ExecuteSqlRawAsync(
            """
            EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

            DECLARE @sql nvarchar(max) = N'';

            SELECT @sql = @sql + N'DELETE FROM ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';'
            FROM sys.tables
            WHERE is_ms_shipped = 0
              AND name <> '__EFMigrationsHistory';

            EXEC sp_executesql @sql;

            EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
            """);
    }

    /// <summary>
    /// A scope of its own, with its own <see cref="AppDbContext"/> — which is what a request gets.
    /// Concurrency tests take several at once, because two scopes are what two requests are.
    /// </summary>
    public AsyncServiceScope CreateScope() =>
        (_provider ?? throw new InvalidOperationException("The fixture has not been initialised."))
            .CreateAsyncScope();

    /// <summary>
    /// Fails early and says exactly what to do, rather than letting every test fail with a
    /// connection error that says nothing about the test suite needing a database of its own.
    /// </summary>
    private async Task EnsureReachableAsync()
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                $"""
                 The integration tests need a database of their own and could not open one.

                 Tried: {Redact(ConnectionString)}

                 Create it once:
                     sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "CREATE DATABASE [{DatabaseName}]"

                 The tests migrate it themselves and never drop it. To point them somewhere else,
                 set {ConnectionVariable} to a full connection string.
                 """,
                ex);
        }
    }

    public async Task DisposeAsync()
    {
        // The database stays. It is yours, it is disposable, and being able to open it after a
        // failure is worth more than leaving the server tidy.
        if (_provider is not null)
            await _provider.DisposeAsync();
    }

    /// <summary>Keeps a password out of the message when the server is configured by hand.</summary>
    private static string Redact(string connectionString) =>
        new SqlConnectionStringBuilder(connectionString) { Password = string.Empty }.ConnectionString;
}

/// <summary>
/// One database for the whole run. Tests isolate themselves by working on a company of their own
/// rather than by emptying tables, which is both cheaper and closer to how the system is used.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server";
}

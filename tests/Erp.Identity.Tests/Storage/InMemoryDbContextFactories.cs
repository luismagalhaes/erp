using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Options;
using Erp.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Identity.Tests.Storage;

/// <summary>
/// Creates isolated in-memory database contexts so the storage implementations
/// can be exercised end to end without a SQL Server instance.
/// </summary>
internal sealed class InMemoryConfigurationDbContextFactory : IDbContextFactory<ConfigurationDbContext>
{
    private readonly DbContextOptions<ConfigurationDbContext> _options;

    public InMemoryConfigurationDbContextFactory()
    {
        // The context reads ConfigurationStoreOptions while building the model, and resolves it
        // from the application service provider — which in the app comes from AddConfigurationDbContext.
        var services = new ServiceCollection()
            .AddSingleton(new ConfigurationStoreOptions())
            .BuildServiceProvider();

        _options = new DbContextOptionsBuilder<ConfigurationDbContext>()
            .UseInMemoryDatabase($"configuration-{Guid.NewGuid()}")
            .UseApplicationServiceProvider(services)
            .Options;
    }

    public ConfigurationDbContext CreateDbContext() => new(_options);
}

internal sealed class InMemoryApplicationDbContextFactory : IDbContextFactory<ApplicationDbContext>
{
    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"identity-{Guid.NewGuid()}")
            .Options;

    public ApplicationDbContext CreateDbContext() => new(_options);
}

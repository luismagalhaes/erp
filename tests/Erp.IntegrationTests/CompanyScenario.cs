using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using Erp.SeriesRegistry.Infrastructure.Application;
using Erp.SeriesRegistry.Infrastructure.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// A company of its own for one test, with whatever it needs to issue documents.
/// </summary>
/// <remarks>
/// The database is shared by the whole run, so isolation comes from the company rather than from
/// emptying tables between tests. Almost everything in this system is scoped by company, which
/// makes that both cheap and true — and it lets the tests run without fighting each other.
/// </remarks>
public sealed class CompanyScenario(SqlServerFixture fixture)
{
    public Guid CompanyId { get; private set; }

    public Guid WarehouseId { get; private set; }

    /// <summary>Runs one piece of work in a scope of its own, as a request would.</summary>
    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = fixture.CreateScope();
        return await work(scope.ServiceProvider);
    }

    public async Task InScopeAsync(Func<IServiceProvider, Task> work)
    {
        await using var scope = fixture.CreateScope();
        await work(scope.ServiceProvider);
    }

    /// <summary>
    /// Tax ids carry a unique index and the database is shared by the run, so each company needs
    /// one of its own. Counted rather than random: a collision would be a confusing failure in an
    /// unrelated test.
    /// </summary>
    private static int _taxIdCounter;

    public async Task<CompanyScenario> CreateAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var taxId = $"5{Interlocked.Increment(ref _taxIdCounter):D8}";

        CompanyId = await InScopeAsync(async services =>
        {
            var companies = services.GetRequiredService<ICompanyAdminService>();

            var company = await companies.CreateAsync(new CreateCompanyRequest(
                $"Empresa {suffix}",
                taxId,
                LegalName: $"Empresa {suffix}, Lda",
                Address: "Rua Um",
                City: "Lisboa",
                PostalCode: "1000-001"));

            return company.Id;
        });

        WarehouseId = await InScopeAsync(async services =>
        {
            var warehouses = services.GetRequiredService<IWarehouseService>();

            var warehouse = await warehouses.CreateAsync(new CreateWarehouseRequest(
                CompanyId, "ARM", "Armazém", IsDefault: true));

            return warehouse.Id;
        });

        return this;
    }

    /// <summary>
    /// A supplier, as the modules receive it: copied onto the document rather than referenced. The
    /// id is invented because Purchasing never looks it up — the host reads it from Core and hands
    /// it over, and that is exactly the shape these tests want to exercise.
    /// </summary>
    public Guid SupplierId { get; } = Guid.NewGuid();

    public PurchaseOrderSupplierDto Supplier { get; } =
        new("F001", "Fornecedor Teste, Lda", "501234567");

    /// <summary>An order placed with the supplier, ready to receive against.</summary>
    public Task<PurchaseOrderDto> GivenPlacedOrderAsync(decimal quantity = 10m, decimal unitPrice = 5m) =>
        InScopeAsync(services =>
            services.GetRequiredService<IPurchaseOrderService>().CreateAsync(
                new CreatePurchaseOrderRequest(
                    CompanyId,
                    SupplierId,
                    Supplier,
                    new DateOnly(2026, 3, 1),
                    WarehouseId,
                    [new PurchaseOrderLineRequest("ART001", "Artigo de teste", quantity, unitPrice)],
                    Place: true),
                "user-1"));

    /// <summary>A series that can issue: created and with a validation code recorded.</summary>
    public Task<Guid> GivenCommunicatedSeriesAsync(string documentType = "FT", string? stockEffect = null) =>
        InScopeAsync(async services =>
        {
            var series = services.GetRequiredService<ISeriesService>();

            var created = await series.CreateAsync(
                new CreateSeriesRequest(
                    CompanyId,
                    documentType,
                    $"{documentType}{Guid.NewGuid().ToString("N")[..6]}",
                    StockEffect: stockEffect),
                "user-1");

            await series.CommunicateAsync(created.Id, "JFTX7RK9");

            return created.Id;
        });
}

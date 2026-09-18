using Erp.Api.Services;
using Erp.Common;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>
/// The sequential codes of master data — 1, 2, 3 — against a real database.
/// </summary>
/// <remarks>
/// The counter is only sequential because its row is locked while the number is taken; a
/// substituted storage hands the same number to every caller and the test would still pass. The
/// unique index on (CompanyId, Code) is the other half, and it only exists in the database.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class MasterDataCodeTests(SqlServerFixture fixture)
{
    private const int Concurrent = 10;

    private static Task<PartnerDto> CreateCustomerAsync(
        IServiceProvider services, Guid companyId, string name, string? code = null) =>
        services.GetRequiredService<MasterDataCodes>().CreateAsync(
            companyId,
            code,
            Constants.CodeCounters.Customers,
            (candidate, ct) => services.GetRequiredService<ICustomerService>().CodeExistsAsync(companyId, candidate, ct),
            candidate => services.GetRequiredService<ICustomerService>().CreateAsync(
                new CreatePartnerRequest(companyId, candidate, name, "999999990")),
            CancellationToken.None);

    [Fact]
    public async Task Customers_created_at_the_same_time_are_numbered_in_sequence()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        var attempts = Enumerable.Range(0, Concurrent).Select(index => Task.Run(async () =>
        {
            await using var scope = fixture.CreateScope();
            var created = await CreateCustomerAsync(scope.ServiceProvider, scenario.CompanyId, $"Cliente {index}");

            return created.Code;
        }));

        var codes = await Task.WhenAll(attempts);

        codes.Should().HaveCount(Concurrent, "no request may be refused for want of a code");
        codes.Select(int.Parse).OrderBy(code => code).Should().Equal(Enumerable.Range(1, Concurrent));
    }

    /// <summary>An import brings its own codes, and the counter steps over whatever they took.</summary>
    [Fact]
    public async Task A_code_that_comes_in_is_kept_and_never_handed_out_again()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        await scenario.InScopeAsync(services => CreateCustomerAsync(services, scenario.CompanyId, "Importado", code: "1"));

        var next = await scenario.InScopeAsync(services => CreateCustomerAsync(services, scenario.CompanyId, "Seguinte"));

        next.Code.Should().Be("2");
    }

    /// <summary>Every kind of master data counts on its own, and so does every company.</summary>
    [Fact]
    public async Task Brands_and_customers_count_separately()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        var customer = await scenario.InScopeAsync(services => CreateCustomerAsync(services, scenario.CompanyId, "Cliente"));

        var brand = await scenario.InScopeAsync(services =>
            services.GetRequiredService<MasterDataCodes>().CreateAsync(
                scenario.CompanyId,
                requestedCode: null,
                Constants.CodeCounters.Brands,
                (candidate, ct) => services.GetRequiredService<IBrandService>().CodeExistsAsync(scenario.CompanyId, candidate, ct),
                candidate => services.GetRequiredService<IBrandService>().CreateAsync(
                    new CreateBrandRequest(scenario.CompanyId, candidate, "Marca")),
                CancellationToken.None));

        customer.Code.Should().Be("1");
        brand.Code.Should().Be("1");
    }

    /// <summary>A company arrives able to hold stock: without a warehouse nothing can move.</summary>
    [Fact]
    public async Task A_new_company_comes_with_a_default_warehouse()
    {
        var scenario = await new CompanyScenario(fixture).CreateAsync();

        var warehouses = await scenario.InScopeAsync(services =>
            services.GetRequiredService<IWarehouseService>().GetAllAsync(scenario.CompanyId));

        warehouses.Should().Contain(warehouse => warehouse.Code == Constants.DefaultWarehouse.Code);
    }
}

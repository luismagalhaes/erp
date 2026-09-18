using Erp.Core.Domain;
using Erp.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// AppDbContext's tenant filter is a defense in depth safeguard behind the API's own authorization
/// (RequireCompanyAccessFilter), and it is the trickiest piece of code in this whole change: the
/// filter is built once, when EF Core builds and caches the model, yet it has to see a different
/// caller on every one of the many AppDbContext instances built afterwards from that same cached
/// model — one per request. Nothing about that can be trusted from reading the code; it is only
/// proven by two real instances, built from the one options object every request actually shares,
/// disagreeing about what they can see against a real database.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class TenantIsolationTests(SqlServerFixture fixture)
{
    /// <summary>Builds an AppDbContext by hand, the way SalesModelConfigurationTests does, with
    /// whichever ICurrentUserContext the test wants — never the one DI would give it.</summary>
    private async Task<AppDbContext> BuildContextAsync(IServiceProvider services, ICurrentUserContext currentUser)
    {
        var options = services.GetRequiredService<DbContextOptions<AppDbContext>>();
        var modules = services.GetRequiredService<IEnumerable<IModuleModelConfiguration>>();
        var context = new AppDbContext(options, modules, currentUser);

        // Forces the model to actually build here if no other instance already has — the whole
        // point is to prove the filter still works on an instance built well after that happened.
        await context.Database.CanConnectAsync();

        return context;
    }

    private static async Task<Guid> GivenBrandAsync(SqlServerFixture fixture, Guid companyId, string code)
    {
        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var brand = new Brand { CompanyId = companyId, Code = code, Name = $"Marca {code}" };
        db.Add(brand);
        await db.SaveChangesAsync();

        return brand.Id;
    }

    private sealed class FakeCurrentUserContext(bool isSuperAdmin, params Guid[] allowedCompanyIds) : ICurrentUserContext
    {
        public bool IsHttpRequest => true;
        public bool IsSuperAdmin => isSuperAdmin;
        public IReadOnlyCollection<Guid> AllowedCompanyIds => allowedCompanyIds;
    }

    [Fact]
    public async Task A_caller_restricted_to_one_company_never_sees_another_companys_rows()
    {
        var companyA = (await new CompanyScenario(fixture).CreateAsync()).CompanyId;
        var companyB = (await new CompanyScenario(fixture).CreateAsync()).CompanyId;

        await GivenBrandAsync(fixture, companyA, "MA");
        await GivenBrandAsync(fixture, companyB, "MB");

        await using var scope = fixture.CreateScope();

        await using var asCompanyA = await BuildContextAsync(scope.ServiceProvider, new FakeCurrentUserContext(false, companyA));
        // Deliberately unfiltered: the point is that the global filter does the restricting, not a
        // Where clause this test adds itself.
        var visibleToA = await asCompanyA.Set<Brand>().Select(b => b.CompanyId).ToListAsync();

        visibleToA.Should().Contain(companyA);
        visibleToA.Should().NotContain(companyB);

        await using var asCompanyB = await BuildContextAsync(scope.ServiceProvider, new FakeCurrentUserContext(false, companyB));
        var visibleToB = await asCompanyB.Set<Brand>().Select(b => b.CompanyId).ToListAsync();

        visibleToB.Should().Contain(companyB);
        visibleToB.Should().NotContain(companyA);
    }

    /// <summary>Two different callers, two different AppDbContext instances, built from the model
    /// EF cached once — this is what actually proves the filter rebinds per instance instead of
    /// freezing whichever caller happened to build the model first.</summary>
    [Fact]
    public async Task Two_contexts_built_from_the_one_cached_model_still_disagree_about_what_they_can_see()
    {
        var companyA = (await new CompanyScenario(fixture).CreateAsync()).CompanyId;
        var companyB = (await new CompanyScenario(fixture).CreateAsync()).CompanyId;

        await GivenBrandAsync(fixture, companyA, "CA");
        await GivenBrandAsync(fixture, companyB, "CB");

        await using var scope = fixture.CreateScope();

        // Building A's context first is what forces the shared model to exist (if it did not
        // already) with A's context as "the one that happened to build it" — exactly the scenario
        // that would leak A's identity into every later context if the filter were not truly
        // per-instance.
        await using var asCompanyA = await BuildContextAsync(scope.ServiceProvider, new FakeCurrentUserContext(false, companyA));
        await using var asCompanyB = await BuildContextAsync(scope.ServiceProvider, new FakeCurrentUserContext(false, companyB));

        (await asCompanyA.Set<Brand>().Select(b => b.CompanyId).ToListAsync()).Should().BeEquivalentTo([companyA]);
        (await asCompanyB.Set<Brand>().Select(b => b.CompanyId).ToListAsync()).Should().BeEquivalentTo([companyB]);
    }

    [Fact]
    public async Task A_SuperAdmin_sees_every_companys_rows()
    {
        var companyA = (await new CompanyScenario(fixture).CreateAsync()).CompanyId;
        var companyB = (await new CompanyScenario(fixture).CreateAsync()).CompanyId;

        await GivenBrandAsync(fixture, companyA, "SA");
        await GivenBrandAsync(fixture, companyB, "SB");

        await using var scope = fixture.CreateScope();
        await using var asSuperAdmin = await BuildContextAsync(scope.ServiceProvider, new FakeCurrentUserContext(true));

        var visible = await asSuperAdmin.Set<Brand>().Select(b => b.CompanyId).ToListAsync();

        visible.Should().Contain([companyA, companyB]);
    }

    /// <summary>The membership table itself must never be caught by its own filter — see
    /// SkipTenantFilterAttribute on UserCompany — or a caller could never find their own memberships
    /// in the first place, since that lookup is what the filter would need answered first.</summary>
    [Fact]
    public async Task UserCompany_rows_are_never_filtered_by_company()
    {
        var company = (await new CompanyScenario(fixture).CreateAsync()).CompanyId;

        await using var scope = fixture.CreateScope();
        var setup = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        setup.Add(new UserCompany { UserId = "user-x", CompanyId = company, Role = "Owner" });
        await setup.SaveChangesAsync();

        // Restricted to a company that is NOT the membership's own — if UserCompany were filtered
        // like everything else, this would come back empty.
        await using var restricted = await BuildContextAsync(scope.ServiceProvider, new FakeCurrentUserContext(false, Guid.NewGuid()));

        var found = await restricted.Set<UserCompany>().AnyAsync(uc => uc.UserId == "user-x" && uc.CompanyId == company);

        found.Should().BeTrue();
    }

    [Fact]
    public async Task Saving_a_row_for_a_company_the_caller_does_not_belong_to_is_refused()
    {
        var companyA = (await new CompanyScenario(fixture).CreateAsync()).CompanyId;
        var companyB = (await new CompanyScenario(fixture).CreateAsync()).CompanyId;

        await using var scope = fixture.CreateScope();
        await using var asCompanyA = await BuildContextAsync(scope.ServiceProvider, new FakeCurrentUserContext(false, companyA));

        asCompanyA.Add(new Brand { CompanyId = companyB, Code = "XX", Name = "Não devia gravar" });

        var act = () => asCompanyA.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not belong to*");
    }

    [Fact]
    public async Task Saving_a_row_for_the_caller_own_company_still_works()
    {
        var companyA = (await new CompanyScenario(fixture).CreateAsync()).CompanyId;

        await using var scope = fixture.CreateScope();
        await using var asCompanyA = await BuildContextAsync(scope.ServiceProvider, new FakeCurrentUserContext(false, companyA));

        asCompanyA.Add(new Brand { CompanyId = companyA, Code = "OK", Name = "Devia gravar" });
        await asCompanyA.SaveChangesAsync();

        var saved = await asCompanyA.Set<Brand>().AnyAsync(b => b.Code == "OK" && b.CompanyId == companyA);
        saved.Should().BeTrue();
    }

    /// <summary>
    /// ExistsForAnotherCompanyAsync is the one place allowed to look past the tenant filter, built
    /// entirely by reflection (Set&lt;T&gt;, IgnoreQueryFilters&lt;T&gt;, AnyAsync&lt;T&gt;, each
    /// resolved by MakeGenericMethod) — exactly the kind of code that compiles cleanly and still
    /// picks the wrong overload, or the wrong entity type, only at runtime. A real database is what
    /// actually proves which one it found.
    /// </summary>
    [Fact]
    public async Task ExistsForAnotherCompanyAsync_finds_a_row_the_tenant_filter_would_hide()
    {
        var companyA = (await new CompanyScenario(fixture).CreateAsync()).CompanyId;
        var companyB = (await new CompanyScenario(fixture).CreateAsync()).CompanyId;

        var brandId = await GivenBrandAsync(fixture, companyB, "EX");

        await using var scope = fixture.CreateScope();
        var checker = (ITenantExistenceChecker)await BuildContextAsync(
            scope.ServiceProvider, new FakeCurrentUserContext(false, companyA));

        var existsForAnother = await checker.ExistsForAnotherCompanyAsync(typeof(Brand), brandId, CancellationToken.None);

        existsForAnother.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsForAnotherCompanyAsync_returns_false_for_an_id_that_truly_does_not_exist()
    {
        var companyA = (await new CompanyScenario(fixture).CreateAsync()).CompanyId;

        await using var scope = fixture.CreateScope();
        var checker = (ITenantExistenceChecker)await BuildContextAsync(
            scope.ServiceProvider, new FakeCurrentUserContext(false, companyA));

        var exists = await checker.ExistsForAnotherCompanyAsync(typeof(Brand), Guid.NewGuid(), CancellationToken.None);

        exists.Should().BeFalse();
    }

    /// <summary>Two entity types can, in principle, reuse a Guid — the check must key off the
    /// entity type it was actually asked about, not just the id.</summary>
    [Fact]
    public async Task ExistsForAnotherCompanyAsync_does_not_match_a_different_entity_type()
    {
        var companyA = (await new CompanyScenario(fixture).CreateAsync()).CompanyId;
        var companyB = (await new CompanyScenario(fixture).CreateAsync()).CompanyId;

        var brandId = await GivenBrandAsync(fixture, companyB, "TY");

        await using var scope = fixture.CreateScope();
        var checker = (ITenantExistenceChecker)await BuildContextAsync(
            scope.ServiceProvider, new FakeCurrentUserContext(false, companyA));

        var existsAsWarehouse = await checker.ExistsForAnotherCompanyAsync(typeof(Warehouse), brandId, CancellationToken.None);

        existsAsWarehouse.Should().BeFalse();
    }
}

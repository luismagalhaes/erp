using System.Text.RegularExpressions;
using Erp.E2ETests.Fixtures;
using Erp.E2ETests.Support;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Erp.E2ETests.Core;

/// <summary>
/// Creating a customer is a P0 flow — it's the prerequisite the user asked for by name, and every
/// sales document needs one to exist first.
/// </summary>
[Collection(PlaywrightCollection.Name)]
[Trait("Category", "E2E")]
public sealed class CustomerManagementTests
{
    private readonly PlaywrightFixture _fixture;
    private readonly ITestOutputHelper _output;

    public CustomerManagementTests(PlaywrightFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task Signed_in_user_can_create_a_customer()
    {
        if (!_fixture.Settings.HasTestUser)
        {
            _output.WriteLine("Skipped: no E2E test user configured (see appsettings.local.json).");
            return;
        }

        var baseUrl = _fixture.Settings.MainBaseUrl;
        var page = await _fixture.NewAuthenticatedPageAsync();
        try
        {
            var companyName = await CompanyTestHelper.CreateCompanyWithAccessAsync(
                page, baseUrl, _fixture.Settings.TestUser.Email!, "E2E Customer Co");

            var unique = TestData.UniqueSuffix();
            var customerCode = $"C{unique}";
            var customerName = $"E2E Customer {unique}";
            var taxId = TestData.UniqueDigits();

            await page.GotoAsync(baseUrl + "/customers/new");
            await CompanyTestHelper.SwitchToCompanyAsync(page, companyName);

            // Exact "Código" also matches "Código postal" (substring match); the required-field
            // marker MudBlazor appends means it isn't literally "Código" either — anchor instead.
            await page.GetByLabel(new Regex("^Código\\*?$")).FillAsync(customerCode);
            await page.GetByLabel("Nome").FillAsync(customerName);
            await page.GetByLabel("NIF").FillAsync(taxId);

            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Guardar" }).ClickAsync();

            await page.WaitForURLAsync(url => url.EndsWith("/customers", StringComparison.OrdinalIgnoreCase));
            var listedCustomer = page.GetByText(customerName);
            await listedCustomer.WaitForAsync();
            (await listedCustomer.CountAsync()).Should().BeGreaterThan(0, "the newly created customer should be listed");
        }
        finally
        {
            await PlaywrightFixture.SaveTraceAsync(page, nameof(Signed_in_user_can_create_a_customer));
        }
    }
}

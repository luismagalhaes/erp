using Erp.E2ETests.Fixtures;
using Erp.E2ETests.Support;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Erp.E2ETests.Core;

/// <summary>
/// Creating a company is the P0 flow: almost nothing else in the app works without one, and
/// nothing is seeded by default — every other business-flow test depends on this working.
/// </summary>
[Collection(PlaywrightCollection.Name)]
[Trait("Category", "E2E")]
public sealed class CompanyManagementTests
{
    private readonly PlaywrightFixture _fixture;
    private readonly ITestOutputHelper _output;

    public CompanyManagementTests(PlaywrightFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task Signed_in_user_can_create_a_company()
    {
        if (!_fixture.Settings.HasTestUser)
        {
            _output.WriteLine("Skipped: no E2E test user configured (see appsettings.local.json).");
            return;
        }

        var page = await _fixture.NewAuthenticatedPageAsync();
        try
        {
            var name = $"E2E Company {TestData.UniqueSuffix()}";
            var taxId = TestData.UniqueDigits();

            await page.GotoAsync(_fixture.Settings.MainBaseUrl + "/companies/new");
            await page.GetByLabel("Nome").FillAsync(name);
            var taxIdField = page.GetByLabel("NIF");
            await taxIdField.FillAsync(taxId);
            // MudTextField binds on blur (not Immediate here) — Fill alone never dispatches it. A
            // direct Blur is more reliable than simulating Tab, whose focus target can vary with timing.
            await taxIdField.BlurAsync();
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Guardar" }).ClickAsync();

            await page.WaitForURLAsync(url => System.Text.RegularExpressions.Regex.IsMatch(url, "/companies/[0-9a-fA-F-]{36}$"));
            await page.GetByText("Empresa criada").WaitForAsync();

            await page.GotoAsync(_fixture.Settings.MainBaseUrl + "/companies");
            var listedCompany = page.GetByText(name);
            await listedCompany.WaitForAsync();
            (await listedCompany.CountAsync()).Should().BeGreaterThan(0, "the newly created company should be listed");
        }
        finally
        {
            await PlaywrightFixture.SaveTraceAsync(page, nameof(Signed_in_user_can_create_a_company));
        }
    }
}

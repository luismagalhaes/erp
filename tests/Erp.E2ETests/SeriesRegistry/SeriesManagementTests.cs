using Erp.E2ETests.Fixtures;
using Erp.E2ETests.Support;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Erp.E2ETests.SeriesRegistry;

/// <summary>
/// Creating a series is a P0 flow — document numbering (invoices, movements, receipts) depends
/// entirely on one existing, and it is also one of the areas the integration test suite has
/// already caught real concurrency bugs in (see tests/Erp.IntegrationTests).
/// </summary>
[Collection(PlaywrightCollection.Name)]
[Trait("Category", "E2E")]
public sealed class SeriesManagementTests
{
    private readonly PlaywrightFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SeriesManagementTests(PlaywrightFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task Signed_in_user_can_create_a_series()
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
                page, baseUrl, _fixture.Settings.TestUser.Email!, "E2E Series Co");

            var seriesCode = $"E2E{TestData.UniqueSuffix()}";

            await page.GotoAsync(baseUrl + "/series/new");
            await CompanyTestHelper.SwitchToCompanyAsync(page, companyName);

            // Document type defaults to FT (Fatura) and the initial sequence to 1 — both fine for
            // this test, so only the series identifier needs filling in.
            var seriesCodeField = page.GetByLabel("Identificador da série");
            await seriesCodeField.FillAsync(seriesCode);
            // Not an Immediate field — Fill alone never dispatches the blur-bound update. A direct
            // Blur is more reliable than simulating Tab, whose focus target can vary with timing.
            await seriesCodeField.BlurAsync();

            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Criar série" }).ClickAsync();

            await page.WaitForURLAsync(url => System.Text.RegularExpressions.Regex.IsMatch(url, "/series/[0-9a-fA-F-]{36}$"));
            await page.GetByText("criada").WaitForAsync();
            var listedSeriesCode = page.GetByText(seriesCode);
            await listedSeriesCode.First.WaitForAsync();
            (await listedSeriesCode.CountAsync()).Should().BeGreaterThan(0, "the newly created series' identifier should be shown");
        }
        finally
        {
            await PlaywrightFixture.SaveTraceAsync(page, nameof(Signed_in_user_can_create_a_series));
        }
    }
}

using Erp.E2ETests.Fixtures;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Erp.E2ETests.Core;

/// <summary>
/// VAT rates are seeded once by migration (not created per-test), so this test edits a known
/// seeded row and reverts it at the end — unlike the rest of the suite, it can't rely on unique
/// per-test data to avoid collisions with other runs.
/// </summary>
[Collection(PlaywrightCollection.Name)]
[Trait("Category", "E2E")]
public sealed class VatRatesTests
{
    // Seeded by the AddVatRates migration: Açores / Reduzida, 4.00%.
    private const string SeededRateId = "9f5a1a10-0002-4a00-8000-000000000003";
    private const string OriginalPercentage = "4";
    private const string UpdatedPercentage = "7";

    private readonly PlaywrightFixture _fixture;
    private readonly ITestOutputHelper _output;

    public VatRatesTests(PlaywrightFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task Backoffice_user_can_see_and_edit_a_seeded_vat_rate()
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
            await page.GotoAsync(baseUrl + "/vat-rates");
            await page.GetByText("Taxas de IVA").First.WaitForAsync();
            // The 12 rows seeded by the AddVatRates migration (3 regions x 4 rates each).
            (await page.Locator("table tbody tr").CountAsync()).Should().Be(12);

            await page.GotoAsync($"{baseUrl}/vat-rates/{SeededRateId}");
            await page.GetByText("Açores — Reduzida").WaitForAsync();

            var percentageField = page.GetByLabel("Percentagem");
            await percentageField.FillAsync(UpdatedPercentage);
            await percentageField.BlurAsync();
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Guardar" }).ClickAsync();

            await page.WaitForURLAsync(url => url.EndsWith("/vat-rates", StringComparison.OrdinalIgnoreCase));
            await page.GetByText("Taxa de IVA atualizada").WaitForAsync();

            // Reload the editor to confirm the new percentage round-tripped through the API.
            await page.GotoAsync($"{baseUrl}/vat-rates/{SeededRateId}");
            await page.GetByText("Açores — Reduzida").WaitForAsync();
            var reloadedValue = await page.GetByLabel("Percentagem").InputValueAsync();
            reloadedValue.Should().StartWith(UpdatedPercentage);
        }
        finally
        {
            // Restore the seeded value so other tests and manual QA see the original data.
            await page.GotoAsync($"{baseUrl}/vat-rates/{SeededRateId}");
            var percentageField = page.GetByLabel("Percentagem");
            await percentageField.FillAsync(OriginalPercentage);
            await percentageField.BlurAsync();
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Guardar" }).ClickAsync();
            await page.WaitForURLAsync(url => url.EndsWith("/vat-rates", StringComparison.OrdinalIgnoreCase));

            await PlaywrightFixture.SaveTraceAsync(page, nameof(Backoffice_user_can_see_and_edit_a_seeded_vat_rate));
        }
    }
}

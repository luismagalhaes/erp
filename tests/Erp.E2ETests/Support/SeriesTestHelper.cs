using Microsoft.Playwright;

namespace Erp.E2ETests.Support;

/// <summary>Creates a series for flows that need one already in place (stock movements, invoices, receipts).</summary>
public static class SeriesTestHelper
{
    /// <summary>
    /// Navigates to the new-series page for the given company (re-selecting it, since this is a
    /// fresh page load and a fresh Blazor circuit) and creates a series. Leaves the document type
    /// at its default (FT) unless <paramref name="documentTypeOptionText"/> is given.
    /// </summary>
    public static async Task<string> CreateSeriesAsync(IPage page, string baseUrl, string companyName, string seriesCodePrefix, string? documentTypeOptionText = null)
    {
        var seriesCode = $"{seriesCodePrefix}{TestData.UniqueSuffix()}";

        await page.GotoAsync(baseUrl + "/series/new");
        await CompanyTestHelper.SwitchToCompanyAsync(page, companyName);

        if (documentTypeOptionText is not null)
        {
            await page.SelectMudOptionAsync(page.MudSelectByLabel("Tipo de documento"), documentTypeOptionText);
        }

        var seriesCodeField = page.GetByLabel("Identificador da série");
        await seriesCodeField.FillAsync(seriesCode);
        // A direct Blur is more reliable than simulating Tab, whose focus target can vary with timing.
        await seriesCodeField.BlurAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Criar série" }).ClickAsync();
        await page.WaitForURLAsync(url => System.Text.RegularExpressions.Regex.IsMatch(url, "/series/[0-9a-fA-F-]{36}$"));

        // A new series can't issue anything (CanIssue is false) until it is "communicated" — in
        // production that means the accountant registers a real code obtained from the AT
        // webservice; this endpoint just records whatever string it's given, no real webservice
        // call, so any value unblocks it here.
        var validationCodeField = page.GetByLabel("Código de validação");
        await validationCodeField.FillAsync($"E2E-{TestData.UniqueSuffix()}");
        await validationCodeField.BlurAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Registar código" }).ClickAsync();
        await page.GetByText("registado").WaitForAsync();

        return seriesCode;
    }
}

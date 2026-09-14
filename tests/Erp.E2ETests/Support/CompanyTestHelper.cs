using Microsoft.Playwright;

namespace Erp.E2ETests.Support;

/// <summary>
/// Almost everything in Erp.Main is scoped to a company, and nothing is seeded by default — every
/// business-flow test starts by creating its own, the same way tests/Erp.IntegrationTests gives
/// each test its own company for isolation.
/// </summary>
public static class CompanyTestHelper
{
    /// <summary>
    /// Creates a company and grants the current user access to it. Creating a company does not do
    /// this automatically — GetMyCompanies only returns companies with an explicit membership, so
    /// without this step the new company would never appear anywhere else in the app.
    /// </summary>
    /// <returns>The company's generated name, unique to this call — pass it to <see cref="SwitchToCompanyAsync"/>.</returns>
    public static async Task<string> CreateCompanyWithAccessAsync(IPage page, string baseUrl, string testUserEmail, string namePrefix)
    {
        var name = $"{namePrefix} {TestData.UniqueSuffix()}";
        var taxId = TestData.UniqueDigits();

        await page.GotoAsync($"{baseUrl}/companies/new");
        await page.GetByLabel("Nome").FillAsync(name);
        var taxIdField = page.GetByLabel("NIF");
        await taxIdField.FillAsync(taxId);
        // MudTextField binds on blur here (not Immediate) — Fill alone never dispatches it. A
        // direct Blur is more reliable than simulating Tab, whose focus target can vary with timing.
        await taxIdField.BlurAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await page.WaitForURLAsync(url => System.Text.RegularExpressions.Regex.IsMatch(url, "/companies/[0-9a-fA-F-]{36}$"));

        await page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "Utilizadores" }).ClickAsync();
        await page.SelectMudOptionAsync(page.MudSelectByLabel("Utilizador"), testUserEmail);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Associar" }).ClickAsync();
        await page.GetByText(testUserEmail).First.WaitForAsync();

        return name;
    }

    /// <summary>
    /// Picks a company in the header's company switcher. Needed on every fresh navigation: a full
    /// page load starts a new Blazor circuit, and CompanyState defaults to the user's first company
    /// by whatever order the API returns them in — not necessarily the one this test just created,
    /// since companies from earlier test runs are never cleaned up.
    /// </summary>
    public static async Task SwitchToCompanyAsync(IPage page, string companyName)
    {
        await page.SelectMudOptionAsync(page.Locator(".company-picker"), companyName);
    }
}

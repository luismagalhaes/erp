using Microsoft.Playwright;

namespace Erp.E2ETests.Support;

/// <summary>
/// MudBlazor's MudSelect renders its bound value on a `type="hidden"` input, so
/// <c>Page.GetByLabel</c> never finds it (hidden inputs are excluded from the accessibility tree)
/// — the field's `label for=` points at that hidden input, not at the clickable element next to
/// it. These helpers work directly with the popover MudSelect actually opens instead.
/// </summary>
public static class MudSelectExtensions
{
    /// <summary>A MudSelect field identified by its visible label text (e.g. "Tipo de documento").</summary>
    public static ILocator MudSelectByLabel(this IPage page, string labelText) =>
        page.Locator(".mud-select").Filter(new LocatorFilterOptions
        {
            Has = page.Locator("label", new PageLocatorOptions { HasText = labelText }),
        });

    /// <summary>Opens a MudSelect and clicks the popover item containing the given text.</summary>
    public static async Task SelectMudOptionAsync(this IPage page, ILocator selectContainer, string optionText)
    {
        await selectContainer.Locator("[tabindex='0']").First.ClickAsync();
        await page.Locator(".mud-popover-open .mud-list-item", new PageLocatorOptions { HasText = optionText })
            .First.ClickAsync();
    }
}

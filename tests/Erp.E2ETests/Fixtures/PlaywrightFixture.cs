using Erp.E2ETests.Configuration;
using Microsoft.Playwright;

namespace Erp.E2ETests.Fixtures;

public sealed class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public E2ESettings Settings { get; } = E2ESettings.Load();

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = Settings.Headless,
            SlowMo = Settings.Headless ? 0 : 300,
        });
    }

    public async Task<IPage> NewPageAsync()
    {
        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
        });
        return await context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}

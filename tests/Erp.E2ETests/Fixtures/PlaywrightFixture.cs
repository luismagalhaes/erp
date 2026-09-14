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
        await context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true,
        });
        return await context.NewPageAsync();
    }

    // Every test records a trace (screenshots, DOM snapshots, network, console) — .NET Playwright
    // has no equivalent to the TypeScript test runner's UI Mode, so this is the closest thing to
    // Cypress's step-by-step run history: open the .zip at https://trace.playwright.dev or with
    // `npx playwright show-trace <path>`.
    public static async Task SaveTraceAsync(IPage page, string testName)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "playwright-traces");
        Directory.CreateDirectory(directory);
        await page.Context.Tracing.StopAsync(new TracingStopOptions
        {
            Path = Path.Combine(directory, $"{testName}.zip"),
        });
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}

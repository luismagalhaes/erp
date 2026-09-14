using Allure.Net.Commons;
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
            // Without this Chromium sends an English Accept-Language header; Erp.Main falls back
            // to it whenever the authenticated user's "locale" claim isn't present, which renders
            // every label in this suite's Portuguese-language selectors as English instead.
            Locale = "pt-PT",
        });
        await context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true,
        });
        return await context.NewPageAsync();
    }

    /// <summary>
    /// Logs the configured test user in and lands back on Erp.Main. Callers must check
    /// <see cref="Configuration.E2ESettings.HasTestUser"/> first — this throws if the credentials
    /// are not configured, since business-flow tests have no meaningful way to run without a user.
    /// </summary>
    public async Task<IPage> NewAuthenticatedPageAsync()
    {
        var page = await NewPageAsync();
        await page.GotoAsync(Settings.MainBaseUrl);
        await page.Locator("input[name='email']").FillAsync(Settings.TestUser.Email!);
        await page.Locator("input[name='password']").FillAsync(Settings.TestUser.Password!);
        await page.Locator("button[type='submit']").ClickAsync();
        await page.WaitForURLAsync(url => url.StartsWith(Settings.MainBaseUrl, StringComparison.OrdinalIgnoreCase));
        return page;
    }

    // Every test records a trace (screenshots, DOM snapshots, network, console) — .NET Playwright
    // has no equivalent to the TypeScript test runner's UI Mode, so this is the closest thing to
    // Cypress's step-by-step run history: open the .zip at https://trace.playwright.dev or with
    // `npx playwright show-trace <path>`.
    public static async Task SaveTraceAsync(IPage page, string testName)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "playwright-traces");
        Directory.CreateDirectory(directory);
        var tracePath = Path.Combine(directory, $"{testName}.zip");
        await page.Context.Tracing.StopAsync(new TracingStopOptions
        {
            Path = tracePath,
        });
        AllureApi.AddAttachment("Playwright trace", "application/zip", tracePath);
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}

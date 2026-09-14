using Erp.E2ETests.Fixtures;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Erp.E2ETests.Identity;

[Collection(PlaywrightCollection.Name)]
[Trait("Category", "E2E")]
public sealed class SignInFlowTests
{
    private readonly PlaywrightFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SignInFlowTests(PlaywrightFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task Anonymous_user_visiting_main_is_redirected_to_identity_login()
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await page.GotoAsync(_fixture.Settings.MainBaseUrl);

            page.Url.Should().Contain("/Account/SignIn");
            await page.Locator("input[name='email']").WaitForAsync();
            await page.Locator("input[name='password']").WaitForAsync();
        }
        finally
        {
            await PlaywrightFixture.SaveTraceAsync(page, nameof(Anonymous_user_visiting_main_is_redirected_to_identity_login));
        }
    }

    [Fact]
    public async Task User_can_sign_in_and_reach_the_dashboard()
    {
        if (!_fixture.Settings.HasTestUser)
        {
            _output.WriteLine("Skipped: no E2E test user configured (see appsettings.local.json).");
            return;
        }

        var page = await _fixture.NewPageAsync();
        try
        {
            await page.GotoAsync(_fixture.Settings.MainBaseUrl);
            await page.FillAsync("input[name='email']", _fixture.Settings.TestUser.Email!);
            await page.FillAsync("input[name='password']", _fixture.Settings.TestUser.Password!);
            await page.ClickAsync("button[type='submit']");

            await page.WaitForURLAsync(url => url.StartsWith(_fixture.Settings.MainBaseUrl, StringComparison.OrdinalIgnoreCase));
            page.Url.Should().NotContain("/Account/SignIn");
        }
        finally
        {
            await PlaywrightFixture.SaveTraceAsync(page, nameof(User_can_sign_in_and_reach_the_dashboard));
        }
    }
}

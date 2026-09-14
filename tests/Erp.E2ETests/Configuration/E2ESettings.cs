using Microsoft.Extensions.Configuration;

namespace Erp.E2ETests.Configuration;

public sealed class E2ESettings
{
    public required string MainBaseUrl { get; init; }
    public required string IdentityBaseUrl { get; init; }
    public bool Headless { get; init; } = true;
    public TestUserSettings TestUser { get; init; } = new();

    public bool HasTestUser => !string.IsNullOrWhiteSpace(TestUser.Email) && !string.IsNullOrWhiteSpace(TestUser.Password);

    public static E2ESettings Load()
    {
        // No prefix here: environment variables override nested keys through the standard
        // ASP.NET Core "Section__Key" convention (e.g. E2E__MainBaseUrl -> E2E:MainBaseUrl), so a
        // prefix would strip "E2E_" and land values at the configuration root instead of under
        // the "E2E" section this class binds to.
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetSection("E2E").Get<E2ESettings>()
            ?? throw new InvalidOperationException("Missing 'E2E' configuration section.");
    }
}

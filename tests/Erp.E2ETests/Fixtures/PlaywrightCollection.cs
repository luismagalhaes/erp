namespace Erp.E2ETests.Fixtures;

[CollectionDefinition(Name)]
public sealed class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "Playwright";
}

using Microsoft.Extensions.Configuration;

namespace Erp.Common.Configuration;

/// <summary>Where to find one host's secrets in Infisical, and how to authenticate to fetch them.</summary>
internal sealed class InfisicalConfigurationSource : IConfigurationSource
{
    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }

    public required string ProjectId { get; init; }

    public required string EnvironmentSlug { get; init; }

    public required string SecretPath { get; init; }

    public string? SiteUrl { get; init; }

    public IConfigurationProvider Build(IConfigurationBuilder builder) => new InfisicalConfigurationProvider(this);
}

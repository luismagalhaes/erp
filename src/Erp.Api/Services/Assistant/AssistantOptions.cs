namespace Erp.Api.Services.Assistant;

/// <summary>
/// Settings of the chat assistant, bound from the <c>Assistant</c> section. The assistant is
/// optional: without an <see cref="ApiKey"/> the host starts as usual and the endpoint answers
/// that the feature is not configured.
/// </summary>
public sealed class AssistantOptions
{
    public const string SectionName = "Assistant";

    /// <summary>Anthropic API key. Vault secret <c>Assistant:ApiKey</c>, never in appsettings.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Model that answers. Plain configuration, so it can change without a deploy of code.</summary>
    public string Model { get; set; } = "claude-opus-5";

    /// <summary>Upper bound for one answer, in tokens.</summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// How many times the model may call tools inside one answer. A ceiling against a loop that
    /// never settles, not something an answer normally gets near.
    /// </summary>
    public int MaxToolRounds { get; set; } = 6;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

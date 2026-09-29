namespace Erp.Api.Services.Assistant;

/// <summary>
/// Settings of the chat assistant, bound from the <c>Assistant</c> section. The assistant talks to
/// any OpenAI-compatible API (Ollama Cloud, a local Ollama, OpenRouter, Gemini), so changing
/// provider or model is configuration only. It is optional: without an endpoint and a model the
/// host starts as usual and the chat answers that the feature is not configured.
/// </summary>
public sealed class AssistantOptions
{
    public const string SectionName = "Assistant";

    /// <summary>Base address of the OpenAI-compatible API.</summary>
    public string? Endpoint { get; set; }

    /// <summary>API key of the provider. Vault secret <c>Assistant:ApiKey</c>, never in appsettings.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Model that answers. Plain configuration: hosted models are retired from time to time.</summary>
    public string? Model { get; set; }

    /// <summary>Upper bound for one answer, in tokens.</summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// How many times the model may call tools inside one answer. A ceiling against a loop that
    /// never settles, not something an answer normally gets near.
    /// </summary>
    public int MaxToolRounds { get; set; } = 6;

    /// <summary>The key is not part of it: a local Ollama needs none.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(Model);
}

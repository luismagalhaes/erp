namespace Erp.Main.Models.Assistant;

/// <summary>Why the assistant gave no answer, so the chat can say it in the user's language.</summary>
public enum AssistantFailure
{
    /// <summary>The API has no key for the model provider, so the feature is switched off.</summary>
    NotConfigured,

    /// <summary>The provider or the API failed, or could not be reached in time.</summary>
    Unavailable,

    /// <summary>The API answered, but with nothing to show.</summary>
    Empty
}

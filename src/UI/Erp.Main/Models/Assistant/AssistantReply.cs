namespace Erp.Main.Models.Assistant;

/// <summary>Either the answer, or the reason there is none.</summary>
public sealed record AssistantReply(string? Text, AssistantFailure? Failure = null);

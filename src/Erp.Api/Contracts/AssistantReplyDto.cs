namespace Erp.Api.Contracts;

/// <summary>What the assistant answered. Empty when the model declined or ran out of rounds.</summary>
public sealed record AssistantReplyDto(string Reply);

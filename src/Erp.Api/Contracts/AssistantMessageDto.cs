namespace Erp.Api.Contracts;

/// <param name="Role">"user" or "assistant".</param>
public sealed record AssistantMessageDto(string Role, string? Content);

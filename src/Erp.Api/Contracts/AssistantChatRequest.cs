namespace Erp.Api.Contracts;

/// <summary>A conversation, oldest message first, ending with what the user just asked.</summary>
public sealed record AssistantChatRequest(IReadOnlyList<AssistantMessageDto> Messages);

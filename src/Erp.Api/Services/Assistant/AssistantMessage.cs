namespace Erp.Api.Services.Assistant;

/// <summary>One turn of the conversation as the UI keeps it: who said it, and what.</summary>
/// <param name="Role">One of <see cref="Erp.Common.Constants.Assistant.MessageRoles"/>.</param>
public sealed record AssistantMessage(string Role, string Content);

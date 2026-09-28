namespace Erp.Main.Models.Assistant;

/// <summary>One turn of the chat. <paramref name="Role"/> is one of <c>Constants.Assistant.MessageRoles</c>.</summary>
public sealed record AssistantMessage(string Role, string Content);

using System.Net;
using System.Net.Http.Json;
using Erp.Main.Models.Assistant;

namespace Erp.Main.Services;

/// <summary>Talks to the chat assistant of the ERP API.</summary>
public sealed class AssistantApiClient(HttpClient http) : ApiClientBase(http)
{
    /// <summary>Sends the conversation and returns the assistant's answer to its last message.</summary>
    public async Task<AssistantReply> ChatAsync(
        Guid companyId,
        IReadOnlyList<AssistantMessage> messages,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Http.PostAsJsonAsync(
                $"api/assistant/chat?companyId={companyId}",
                new { messages },
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                return new AssistantReply(null, AssistantFailure.NotConfigured);

            if (!response.IsSuccessStatusCode)
                return new AssistantReply(null, AssistantFailure.Unavailable);

            var payload = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);

            return string.IsNullOrWhiteSpace(payload?.Reply)
                ? new AssistantReply(null, AssistantFailure.Empty)
                : new AssistantReply(payload.Reply);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // A timeout surfaces as a cancelled task even though nobody cancelled it.
            return new AssistantReply(null, AssistantFailure.Unavailable);
        }
    }

    private sealed record ChatResponse(string? Reply);
}

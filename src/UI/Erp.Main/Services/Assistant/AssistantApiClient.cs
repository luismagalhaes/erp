using System.Net;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using Erp.Common;
using Erp.Main.Models.Assistant;

namespace Erp.Main.Services;

/// <summary>Talks to the chat assistant of the ERP API.</summary>
public sealed class AssistantApiClient(HttpClient http) : ApiClientBase(http)
{
    /// <summary>
    /// Sends the conversation and reads the assistant's answer to its last message while it is
    /// written, handing each piece to <paramref name="onText"/>. Returns the whole answer once it is
    /// complete. If the model fails after it has started, the reply carries what arrived together
    /// with the failure, so the chat can keep the part the user already read.
    /// </summary>
    public async Task<AssistantReply> ChatAsync(
        Guid companyId,
        IReadOnlyList<AssistantMessage> messages,
        Func<string, Task> onText,
        CancellationToken cancellationToken = default)
    {
        var answer = new StringBuilder();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/assistant/chat/stream?companyId={companyId}")
            {
                Content = JsonContent.Create(new { messages })
            };

            // Without ResponseHeadersRead the whole answer is buffered before the first piece is seen.
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                return new AssistantReply(null, AssistantFailure.NotConfigured);

            if (!response.IsSuccessStatusCode)
                return new AssistantReply(null, AssistantFailure.Unavailable);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            await foreach (var item in SseParser.Create(stream).EnumerateAsync(cancellationToken))
            {
                if (item.EventType == Constants.Assistant.StreamEvents.Done)
                    break;

                if (item.EventType == Constants.Assistant.StreamEvents.Error)
                    return Partial(answer, AssistantFailure.Unavailable);

                var piece = JsonSerializer.Deserialize<string>(item.Data);

                if (string.IsNullOrEmpty(piece))
                    continue;

                answer.Append(piece);
                await onText(piece);
            }

            return answer.Length == 0
                ? new AssistantReply(null, AssistantFailure.Empty)
                : new AssistantReply(answer.ToString());
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // A timeout surfaces as a cancelled task even though nobody cancelled it, and a
            // connection cut in the middle of the answer as an IOException.
            return Partial(answer, AssistantFailure.Unavailable);
        }
    }

    private static AssistantReply Partial(StringBuilder answer, AssistantFailure failure) =>
        new(answer.Length == 0 ? null : answer.ToString(), failure);
}

using System.Runtime.CompilerServices;
using Erp.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Erp.Api.Services.Assistant;

/// <summary>
/// Answers a user's question about their company by letting the model call <see cref="AssistantTools"/>
/// until it has the figures it needs.
/// </summary>
/// <remarks>
/// Stateless on purpose: the UI sends the conversation each time, and every answer re-reads the
/// data instead of trusting figures quoted earlier, which may be a day old by the time a chat is
/// picked up again. Nothing here writes to the ERP; the assistant only reads. The tool loop is the
/// function invocation middleware of the <see cref="IChatClient"/>, not code in this class.
/// </remarks>
public sealed class AssistantService(
    IOptions<AssistantOptions> options,
    AssistantTools tools,
    ILogger<AssistantService> logger,
    IChatClient? client = null)
{
    private readonly AssistantOptions _options = options.Value;

    public bool IsConfigured => client is not null && _options.IsConfigured;

    /// <summary>Replies to the last message of <paramref name="history"/>.</summary>
    /// <exception cref="InvalidOperationException">The assistant has no provider configured.</exception>
    public async Task<string> ChatAsync(
        Guid companyId,
        IReadOnlyList<AssistantMessage> history,
        CancellationToken cancellationToken)
    {
        var (chat, messages, chatOptions) = Prepare(companyId, history);

        var response = await chat.GetResponseAsync(messages, chatOptions, cancellationToken);

        if (response.FinishReason == ChatFinishReason.ContentFilter)
            return string.Empty;

        var text = response.Text.Trim();

        if (text.Length == 0)
        {
            logger.LogWarning(
                "The assistant returned no text for company {CompanyId} (finish reason {FinishReason}); it may have hit the {Rounds}-round tool limit.",
                companyId,
                response.FinishReason,
                _options.MaxToolRounds);
        }

        return text;
    }

    /// <summary>
    /// Same as <see cref="ChatAsync"/>, but hands the answer over as the model writes it. Tools run
    /// between the pieces, so the first one may take a while; nothing is yielded meanwhile.
    /// </summary>
    /// <exception cref="InvalidOperationException">The assistant has no provider configured.</exception>
    public async IAsyncEnumerable<string> ChatStreamAsync(
        Guid companyId,
        IReadOnlyList<AssistantMessage> history,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (chat, messages, chatOptions) = Prepare(companyId, history);

        var started = false;

        await foreach (var update in chat.GetStreamingResponseAsync(messages, chatOptions, cancellationToken))
        {
            var piece = update.Text;

            // The blank lines some models open with are not part of the answer.
            if (!started)
                piece = piece.TrimStart();

            if (piece.Length == 0)
                continue;

            started = true;

            yield return piece;
        }

        if (!started)
        {
            logger.LogWarning(
                "The assistant streamed no text for company {CompanyId}; it may have hit the {Rounds}-round tool limit.",
                companyId,
                _options.MaxToolRounds);
        }
    }

    private (IChatClient Client, List<ChatMessage> Messages, ChatOptions Options) Prepare(
        Guid companyId,
        IReadOnlyList<AssistantMessage> history)
    {
        if (client is null || !_options.IsConfigured)
            throw new InvalidOperationException("The assistant is not configured.");

        // The system prompt is its own message and the user's text never joins it.
        List<ChatMessage> messages =
        [
            new(ChatRole.System, BuildSystemPrompt(DateOnly.FromDateTime(DateTime.UtcNow))),
            .. history.Select(message => new ChatMessage(
                message.Role == Constants.Assistant.MessageRoles.Assistant ? ChatRole.Assistant : ChatRole.User,
                message.Content))
        ];

        var options = new ChatOptions
        {
            ModelId = _options.Model,
            MaxOutputTokens = _options.MaxTokens,
            Tools = tools.CreateFunctions(companyId)
        };

        return (client, messages, options);
    }

    private static string BuildSystemPrompt(DateOnly today) =>
        $"""
        You are the assistant built into an ERP for Portuguese companies. Today is {today:yyyy-MM-dd}.
        You help the user understand how their company sells and what to do to sell more.

        Rules:
        - Answer in the language the user writes in; European Portuguese by default.
        - Every figure you state must come from a tool result. Never estimate or invent numbers. If the tools cannot answer something (stock, purchases, costs, margins), say so plainly.
        - Amounts are in euros. "Sales" means net sales, without VAT, with credit notes deducted; say when you show VAT-inclusive figures instead.
        - When the user names a month without a year, assume the current year, or the most recent past occurrence if that month has not happened yet. Resolve "this year", "last year" and "last month" from today's date.
        - The current month is incomplete. Say so when you compare it with a full month, and prefer comparing like with like.
        - To compare months or years, call the tools instead of doing the subtraction yourself.
        - When asked how to sell more, ground every suggestion in the data you fetched: weak months against the same month last year, customers who bought before and stopped, dependence on a few customers. Keep to a few concrete suggestions.
        - You can only read data. You cannot issue, change or delete anything, so never claim you did.
        - Reply in plain text, short and direct. No markdown headings, tables, bold or code formatting; use a hyphen for list items.
        """;
}

using Anthropic;
using Anthropic.Models.Messages;
using Erp.Common;
using Microsoft.Extensions.Options;

namespace Erp.Api.Services.Assistant;

/// <summary>
/// Answers a user's question about their company by letting Claude call <see cref="AssistantTools"/>
/// until it has the figures it needs.
/// </summary>
/// <remarks>
/// Stateless on purpose: the UI sends the conversation each time, and every answer re-reads the
/// data instead of trusting figures quoted earlier, which may be a day old by the time a chat is
/// picked up again. Nothing here writes to the ERP; the assistant only reads.
/// </remarks>
public sealed class AssistantService(
    IOptions<AssistantOptions> options,
    AssistantTools tools,
    ILogger<AssistantService> logger,
    AnthropicClient? client = null)
{
    private readonly AssistantOptions _options = options.Value;

    public bool IsConfigured => client is not null && _options.IsConfigured;

    /// <summary>Replies to the last message of <paramref name="history"/>.</summary>
    /// <exception cref="InvalidOperationException">The assistant has no API key configured.</exception>
    public async Task<string> ChatAsync(
        Guid companyId,
        IReadOnlyList<AssistantMessage> history,
        CancellationToken cancellationToken)
    {
        if (client is null || !_options.IsConfigured)
            throw new InvalidOperationException("The assistant is not configured.");

        List<MessageParam> messages =
        [
            .. history.Select(message => new MessageParam
            {
                Role = message.Role == Constants.Assistant.MessageRoles.Assistant ? Role.Assistant : Role.User,
                Content = message.Content
            })
        ];

        for (var round = 0; round <= _options.MaxToolRounds; round++)
        {
            var response = await client.Messages.Create(
                new MessageCreateParams
                {
                    Model = _options.Model,
                    MaxTokens = _options.MaxTokens,
                    System = BuildSystemPrompt(DateOnly.FromDateTime(DateTime.UtcNow)),
                    Tools = [.. AssistantTools.Definitions],
                    Messages = messages
                },
                cancellationToken);

            if (response.StopReason == StopReason.Refusal)
                return string.Empty;

            List<ContentBlockParam> assistantContent = [];
            List<ContentBlockParam> toolResults = [];
            List<string> text = [];

            foreach (var block in response.Content)
            {
                if (block.TryPickText(out var textBlock))
                {
                    assistantContent.Add(new TextBlockParam { Text = textBlock.Text });
                    text.Add(textBlock.Text);
                }
                else if (block.TryPickThinking(out var thinking))
                {
                    // Sent back untouched, signature included: the API rejects a tampered block.
                    assistantContent.Add(new ThinkingBlockParam
                    {
                        Thinking = thinking.Thinking,
                        Signature = thinking.Signature
                    });
                }
                else if (block.TryPickRedactedThinking(out var redacted))
                {
                    assistantContent.Add(new RedactedThinkingBlockParam { Data = redacted.Data });
                }
                else if (block.TryPickToolUse(out var toolUse))
                {
                    assistantContent.Add(new ToolUseBlockParam
                    {
                        ID = toolUse.ID,
                        Name = toolUse.Name,
                        Input = toolUse.Input
                    });

                    toolResults.Add(await RunToolAsync(companyId, toolUse, cancellationToken));
                }
            }

            if (toolResults.Count == 0)
                return string.Join("\n\n", text).Trim();

            // One result per tool call, all in one message: the API rejects the follow-up otherwise.
            messages.Add(new MessageParam { Role = Role.Assistant, Content = assistantContent });
            messages.Add(new MessageParam { Role = Role.User, Content = toolResults });
        }

        logger.LogWarning(
            "The assistant was still calling tools after {Rounds} rounds for company {CompanyId}; giving up.",
            _options.MaxToolRounds,
            companyId);

        return string.Empty;
    }

    private async Task<ToolResultBlockParam> RunToolAsync(
        Guid companyId,
        ToolUseBlock toolUse,
        CancellationToken cancellationToken)
    {
        try
        {
            return new ToolResultBlockParam
            {
                ToolUseID = toolUse.ID,
                Content = await tools.ExecuteAsync(companyId, toolUse.Name, toolUse.Input, cancellationToken)
            };
        }
        catch (ArgumentException ex)
        {
            // The model's mistake, not ours: say what was wrong so it can ask again.
            return new ToolResultBlockParam { ToolUseID = toolUse.ID, Content = ex.Message, IsError = true };
        }
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

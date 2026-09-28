using Erp.Api.Contracts;
using Erp.Api.Security;
using Erp.Api.Services;
using Erp.Api.Services.Assistant;
using Erp.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Assistant;

/// <summary>The chat assistant that answers questions about the company's data.</summary>
[ApiController]
[Route("api/assistant")]
[Authorize]
[Produces("application/json")]
public sealed class AssistantController(
    AssistantService assistant,
    ILogger<AssistantController> logger) : ControllerBase
{
    /// <summary>
    /// Replies to the last message of a conversation. Read only: the assistant looks at the data and
    /// never changes it, so a company whose subscription lapsed can still ask about its own figures.
    /// </summary>
    [HttpPost("chat")]
    [Authorize(Policy = Policies.Read)]
    [AllowWithoutSubscription]
    [ProducesResponseType<AssistantReplyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AssistantReplyDto>> Chat(
        [FromQuery] Guid companyId,
        [FromBody] AssistantChatRequest request,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        var messages = Normalize(request.Messages);

        if (messages.Count == 0 || messages[^1].Role != Constants.Assistant.MessageRoles.User)
            return BadRequest(new { error = "The conversation must end with a message from the user." });

        if (!assistant.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "The assistant is not configured." });

        try
        {
            var reply = await assistant.ChatAsync(companyId, messages, cancellationToken);

            return Ok(new AssistantReplyDto(reply));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Whatever the provider says (rate limit, outage, bad key) is not for the user to read:
            // it goes to the log and the UI gets one message it can localize.
            logger.LogError(ex, "The assistant failed to answer for company {CompanyId}.", companyId);

            return StatusCode(StatusCodes.Status502BadGateway, new { error = "The assistant could not answer." });
        }
    }

    /// <summary>
    /// Keeps the latest turns, drops empty ones and caps their length, then makes the roles
    /// alternate starting with the user, which is the only shape the model accepts.
    /// </summary>
    private static List<AssistantMessage> Normalize(IReadOnlyList<AssistantMessageDto>? messages)
    {
        var result = new List<AssistantMessage>();

        foreach (var message in (messages ?? []).TakeLast(Constants.Assistant.MaxHistoryMessages))
        {
            var content = message.Content?.Trim();

            if (string.IsNullOrEmpty(content)
                || message.Role is not (Constants.Assistant.MessageRoles.User or Constants.Assistant.MessageRoles.Assistant))
            {
                continue;
            }

            if (content.Length > Constants.Assistant.MaxMessageLength)
                content = content[..Constants.Assistant.MaxMessageLength];

            // Two turns from the same side in a row are merged; a history that opens with the
            // assistant loses that turn.
            if (result.Count == 0 && message.Role != Constants.Assistant.MessageRoles.User)
                continue;

            if (result.Count > 0 && result[^1].Role == message.Role)
                result[^1] = result[^1] with { Content = $"{result[^1].Content}\n{content}" };
            else
                result.Add(new AssistantMessage(message.Role, content));
        }

        return result;
    }
}

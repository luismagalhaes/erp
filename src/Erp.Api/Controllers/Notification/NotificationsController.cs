using Erp.Api.Authorization;
using Erp.Api.Contracts;
using Erp.Notification.Domain.Models;
using Erp.Notification.Infrastructure.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Notification;

/// <summary>Email queue and delivery history.</summary>
[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
public sealed class NotificationsController(
    IEmailNotificationService emailNotificationService,
    IEmailHistoryService emailHistoryService) : ControllerBase
{
    /// <summary>
    /// Queues an email for delivery. Called service to service (for example by the Identity host
    /// on a password reset) with a client credentials token carrying the send scope.
    /// </summary>
    [HttpPost("email")]
    [Authorize(Policy = Policies.NotificationSend)]
    [ProducesResponseType<QueuedEmailDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<QueuedEmailDto>> QueueEmail(
        [FromBody] EmailNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var id = await emailNotificationService.EnqueueAsync(request, cancellationToken);
        return Accepted(new QueuedEmailDto(id));
    }

    /// <summary>Lists every email, most recent first.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<EmailNotificationListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmailNotificationListItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        var history = await emailHistoryService.GetHistoryAsync(cancellationToken);
        return Ok(history.Select(x => x.ToListItem()).ToList());
    }

    /// <summary>Gets one email, including the body that was sent.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<EmailNotificationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmailNotificationDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var notification = await emailHistoryService.GetByIdAsync(id, cancellationToken);
        return notification is null ? NotFound() : Ok(notification.ToDetail());
    }

    /// <summary>Puts a failed email back in the queue. Only failed emails can be requeued.</summary>
    [HttpPost("{id:guid}/requeue")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Requeue(Guid id, CancellationToken cancellationToken)
    {
        var notification = await emailHistoryService.GetByIdAsync(id, cancellationToken);
        if (notification is null)
            return NotFound();

        var requeued = await emailHistoryService.RequeueFailedAsync(id, cancellationToken);

        return requeued
            ? NoContent()
            : Conflict(new { error = "Only a failed email can be requeued." });
    }
}

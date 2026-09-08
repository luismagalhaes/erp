using Erp.Api.Services;
using Erp.Api.Contracts;
using Erp.Notification.Domain.Models;
using Erp.Notification.Infrastructure.Application;
using Erp.Notification.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;

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
    public ActionResult<IReadOnlyList<EmailNotificationListItemDto>> GetAll() =>
        Ok(emailHistoryService.QueryHistory()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToList());

    /// <summary>
    /// The listing the backoffice data grid calls. Filtering, sorting and paging travel as OData
    /// options and are applied by the database. The email queue is global, so there is no tenancy
    /// filter to keep outside the query here.
    /// </summary>
    [HttpGet("odata")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<ODataCollection<EmailNotificationListItemDto>>(StatusCodes.Status200OK)]
    public ActionResult<ODataCollection<EmailNotificationListItemDto>> Query(
        ODataQueryOptions<EmailNotificationListItemDto> options) =>
        Ok(ODataQueryExecutor.Execute(emailHistoryService.QueryHistory(), options));

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

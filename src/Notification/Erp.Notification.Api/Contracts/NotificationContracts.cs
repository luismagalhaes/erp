using Erp.Notification.Domain.Models;

namespace Erp.Notification.Api.Contracts;

public sealed record EmailNotificationListItemDto(
    Guid Id,
    string ToEmail,
    string Subject,
    string Status,
    int RetryCount,
    string? LastError,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);

public sealed record EmailNotificationDetailDto(
    Guid Id,
    string ToEmail,
    string Subject,
    string HtmlBody,
    string Status,
    int RetryCount,
    string? LastError,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);

public sealed record QueuedEmailDto(Guid Id);

public static class NotificationMapping
{
    public static EmailNotificationListItemDto ToListItem(this EmailNotification notification) =>
        new(notification.Id,
            notification.ToEmail,
            notification.Subject,
            notification.Status.ToString(),
            notification.RetryCount,
            notification.LastError,
            notification.CreatedAtUtc,
            notification.ProcessedAtUtc);

    public static EmailNotificationDetailDto ToDetail(this EmailNotification notification) =>
        new(notification.Id,
            notification.ToEmail,
            notification.Subject,
            notification.HtmlBody,
            notification.Status.ToString(),
            notification.RetryCount,
            notification.LastError,
            notification.CreatedAtUtc,
            notification.ProcessedAtUtc);
}

namespace Erp.Main.Models.Notification;

public sealed record NotificationListItem(
    Guid Id,
    string ToEmail,
    string Subject,
    string Status,
    int RetryCount,
    string? LastError,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);

public sealed record NotificationDetail(
    Guid Id,
    string ToEmail,
    string Subject,
    string HtmlBody,
    string Status,
    int RetryCount,
    string? LastError,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);

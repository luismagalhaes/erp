using Erp.Notification.Domain.Models;

namespace Erp.Notification.Infrastructure.Contracts;

/// <summary>
/// The email as the backoffice listing sees it. It lives in the module, and not in the API host,
/// because the projection that feeds the OData grid is written by the storage layer.
/// </summary>
public sealed record EmailNotificationListItemDto
{
    public Guid Id { get; init; }
    public string ToEmail { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;

    /// <summary>
    /// The status as text so the grid can filter it without knowing the numeric values the column
    /// is stored as. The projection maps the enum explicitly for the same reason: calling ToString
    /// on it would not survive the translation to SQL.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    public int RetryCount { get; init; }
    public string? LastError { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ProcessedAtUtc { get; init; }
}

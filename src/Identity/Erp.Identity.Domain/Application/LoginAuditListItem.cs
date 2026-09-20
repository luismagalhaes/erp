namespace Erp.Identity.Domain.Application;

public sealed record LoginAuditListItem(
    Guid Id,
    string? UserId,
    string Email,
    bool Succeeded,
    string? FailureReason,
    string? RemoteIp,
    DateTime OccurredAtUtc);

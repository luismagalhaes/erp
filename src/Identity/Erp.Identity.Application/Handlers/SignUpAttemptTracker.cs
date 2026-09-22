using Erp.Identity.Infrastructure.Application;
using Erp.Identity.Infrastructure.Storage;

namespace Erp.Identity.Application.Handlers;

/// <summary>Thin pass-through to <see cref="ISignUpAttemptStorage"/> — persisted, not in-memory,
/// so the count stays correct across every instance of the host, same as sign-in's own count.</summary>
public sealed class SignUpAttemptTracker(ISignUpAttemptStorage storage) : ISignUpAttemptTracker
{
    public Task<int> GetCountAsync(string? remoteIp, TimeSpan window, CancellationToken cancellationToken = default)
        => storage.CountRecentAsync(remoteIp, window, cancellationToken);

    public Task RecordAttemptAsync(string? remoteIp, CancellationToken cancellationToken = default)
        => storage.RecordAsync(remoteIp, cancellationToken);
}

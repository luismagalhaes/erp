using System.Collections.Concurrent;
using Erp.Identity.Infrastructure.Application;

namespace Erp.Identity.Application.Handlers;

/// <summary>
/// In-memory only — resets on app restart, which is fine for a soft anti-automation signal and
/// avoids a database round trip on every sign-up page render. Registered as a singleton.
/// </summary>
public sealed class SignUpAttemptTracker : ISignUpAttemptTracker
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> _attempts = new();

    public int GetCount(string? remoteIp, TimeSpan window)
    {
        if (!_attempts.TryGetValue(remoteIp ?? "unknown", out var queue))
            return 0;

        Prune(queue, window);
        return queue.Count;
    }

    public void RecordAttempt(string? remoteIp, TimeSpan window)
    {
        var queue = _attempts.GetOrAdd(remoteIp ?? "unknown", static _ => new ConcurrentQueue<DateTime>());
        queue.Enqueue(DateTime.UtcNow);
        Prune(queue, window);
    }

    private static void Prune(ConcurrentQueue<DateTime> queue, TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;
        while (queue.TryPeek(out var oldest) && oldest < cutoff)
            queue.TryDequeue(out _);
    }
}

using System.Collections.Concurrent;

namespace ThirdLambda.Server.Health;

/// <summary>
/// Shared heartbeat registry. Each BackgroundService registers itself with a timeout
/// and calls Beat() every time it completes a loop iteration. The liveness check
/// fails if any registered worker goes quiet for longer than its timeout.
/// </summary>
public sealed class WorkerHeartbeat
{
    private readonly ConcurrentDictionary<string, Entry> _workers = new();

    /// <summary>Register a worker. Registration counts as the first beat, which gives it a startup grace period.</summary>
    public void Register(string name, TimeSpan timeout) =>
        _workers[name] = new Entry(timeout, DateTimeOffset.UtcNow);

    public void Beat(string name)
    {
        if (_workers.TryGetValue(name, out var entry))
            entry.Touch(DateTimeOffset.UtcNow);
    }

    public IReadOnlyList<WorkerStatus> Snapshot() =>
        _workers.Select(kv => new WorkerStatus(kv.Key, kv.Value.Timeout, kv.Value.LastBeat)).ToList();

    public sealed record WorkerStatus(string Name, TimeSpan Timeout, DateTimeOffset LastBeat);

    private sealed class Entry(TimeSpan timeout, DateTimeOffset firstBeat)
    {
        private long _lastBeatTicks = firstBeat.UtcTicks;

        public TimeSpan Timeout { get; } = timeout;

        public DateTimeOffset LastBeat =>
            new(Interlocked.Read(ref _lastBeatTicks), TimeSpan.Zero);

        public void Touch(DateTimeOffset now) =>
            Interlocked.Exchange(ref _lastBeatTicks, now.UtcTicks);
    }
}
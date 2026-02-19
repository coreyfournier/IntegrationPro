using System.Collections.Concurrent;
using IntegrationPro.Application.Interfaces;

namespace IntegrationPro.Infrastructure.HealthChecks;

public sealed class JobStatusStore : IJobStatusStore
{
    private const int MaxCapacity = 50;
    private readonly ConcurrentQueue<JobStatusSnapshot> _statuses = new();
    private volatile int _count;

    public bool HasReceivedAnyJob => _count > 0;

    public void RecordStatus(string requestId, JobStatusCategory status, string description)
    {
        _statuses.Enqueue(new JobStatusSnapshot(requestId, status, description, DateTimeOffset.UtcNow));
        Interlocked.Increment(ref _count);

        while (_statuses.Count > MaxCapacity)
        {
            _statuses.TryDequeue(out _);
        }
    }

    public IReadOnlyList<JobStatusSnapshot> GetRecentStatuses()
    {
        return _statuses.ToArray();
    }

    public JobStatusSnapshot? GetLatestStatus()
    {
        // ToArray gives us a snapshot; take the last element
        var snapshot = _statuses.ToArray();
        return snapshot.Length > 0 ? snapshot[^1] : null;
    }
}

namespace IntegrationPro.Application.Interfaces;

public enum JobStatusCategory
{
    Running,
    Completed,
    Failed
}

public sealed record JobStatusSnapshot(
    string RequestId,
    JobStatusCategory Status,
    string Description,
    DateTimeOffset Timestamp);

public interface IJobStatusStore
{
    void RecordStatus(string requestId, JobStatusCategory status, string description);
    IReadOnlyList<JobStatusSnapshot> GetRecentStatuses();
    JobStatusSnapshot? GetLatestStatus();
    bool HasReceivedAnyJob { get; }
}

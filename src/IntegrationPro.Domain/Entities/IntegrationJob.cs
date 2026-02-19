using IntegrationPro.Domain.Enums;

namespace IntegrationPro.Domain.Entities;

/// <summary>
/// Aggregate root representing the lifecycle of a single integration job.
/// </summary>
public sealed class IntegrationJob
{
    public string RequestId { get; }
    public string PluginName { get; }
    public JobStatus Status { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? ErrorMessage { get; private set; }

    private readonly List<JobProgressEntry> _progressEntries = new();
    public IReadOnlyList<JobProgressEntry> ProgressEntries => _progressEntries.AsReadOnly();

    public IntegrationJob(string requestId, string pluginName)
    {
        RequestId = requestId;
        PluginName = pluginName;
        Status = JobStatus.Pending;
        StartedAtUtc = DateTime.UtcNow;
    }

    public void MarkStarted(string description)
    {
        Status = JobStatus.Running;
        _progressEntries.Add(new JobProgressEntry(0, 0, description, DateTime.UtcNow));
    }

    public void RecordProgress(int currentStep, int totalSteps, string description)
    {
        _progressEntries.Add(new JobProgressEntry(currentStep, totalSteps, description, DateTime.UtcNow));
    }

    public void MarkCompleted(string summary)
    {
        Status = JobStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        _progressEntries.Add(new JobProgressEntry(0, 0, summary, DateTime.UtcNow));
    }

    public void MarkFailed(string errorMessage)
    {
        Status = JobStatus.Failed;
        CompletedAtUtc = DateTime.UtcNow;
        ErrorMessage = errorMessage;
    }
}

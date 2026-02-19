using IntegrationPro.Application.Interfaces;

namespace IntegrationPro.Infrastructure.Progress;

public sealed class HealthTrackingProgressReporter : IProgressReporter
{
    private readonly IProgressReporter _inner;
    private readonly IJobStatusStore _statusStore;

    public HealthTrackingProgressReporter(IProgressReporter inner, IJobStatusStore statusStore)
    {
        _inner = inner;
        _statusStore = statusStore;
    }

    public Task ReportStartedAsync(string requestId, string pluginName, string message)
    {
        _statusStore.RecordStatus(requestId, JobStatusCategory.Running, $"{pluginName}: {message}");
        return _inner.ReportStartedAsync(requestId, pluginName, message);
    }

    public Task ReportProgressAsync(string requestId, int currentStep, int totalSteps, string description)
    {
        // Progress ticks are not recorded to avoid noise in the status store
        return _inner.ReportProgressAsync(requestId, currentStep, totalSteps, description);
    }

    public Task ReportCompletedAsync(string requestId, string summary)
    {
        _statusStore.RecordStatus(requestId, JobStatusCategory.Completed, summary);
        return _inner.ReportCompletedAsync(requestId, summary);
    }

    public Task ReportFailedAsync(string requestId, string errorMessage, Exception? exception)
    {
        _statusStore.RecordStatus(requestId, JobStatusCategory.Failed, errorMessage);
        return _inner.ReportFailedAsync(requestId, errorMessage, exception);
    }
}

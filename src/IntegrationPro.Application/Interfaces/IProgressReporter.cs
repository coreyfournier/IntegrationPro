namespace IntegrationPro.Application.Interfaces;

/// <summary>
/// Abstraction for reporting job progress externally (e.g., to a database, API, or event stream).
/// </summary>
public interface IProgressReporter
{
    Task ReportStartedAsync(string requestId, string pluginName, string message);
    Task ReportProgressAsync(string requestId, int currentStep, int totalSteps, string description);
    Task ReportCompletedAsync(string requestId, string summary);
    Task ReportFailedAsync(string requestId, string errorMessage, Exception? exception);
}

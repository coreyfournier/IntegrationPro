using IntegrationPro.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace IntegrationPro.Infrastructure.Progress;

/// <summary>
/// Reports job progress via structured logging.
/// In production, extend to push progress to a database, SignalR, or external API.
/// </summary>
public sealed class LoggingProgressReporter : IProgressReporter
{
    private readonly ILogger<LoggingProgressReporter> _logger;

    public LoggingProgressReporter(ILogger<LoggingProgressReporter> logger)
    {
        _logger = logger;
    }

    public Task ReportStartedAsync(string requestId, string pluginName, string message)
    {
        _logger.LogInformation("[Progress] {RequestId} | {PluginName} | STARTED: {Message}",
            requestId, pluginName, message);
        return Task.CompletedTask;
    }

    public Task ReportProgressAsync(string requestId, int currentStep, int totalSteps, string description)
    {
        _logger.LogInformation("[Progress] {RequestId} | Step {Current}/{Total}: {Description}",
            requestId, currentStep, totalSteps, description);
        return Task.CompletedTask;
    }

    public Task ReportCompletedAsync(string requestId, string summary)
    {
        _logger.LogInformation("[Progress] {RequestId} | COMPLETED: {Summary}",
            requestId, summary);
        return Task.CompletedTask;
    }

    public Task ReportFailedAsync(string requestId, string errorMessage, Exception? exception)
    {
        _logger.LogError(exception, "[Progress] {RequestId} | FAILED: {ErrorMessage}",
            requestId, errorMessage);
        return Task.CompletedTask;
    }
}

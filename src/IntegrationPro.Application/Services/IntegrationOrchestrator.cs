using IntegrationPro.Application.Interfaces;
using IntegrationPro.Application.PluginLoading;
using IntegrationPro.Domain.Entities;
using IntegrationPro.Domain.Messages;
using IntegrationPro.PluginBase;
using Microsoft.Extensions.Logging;

namespace IntegrationPro.Application.Services;

/// <summary>
/// Orchestrates the full ETL lifecycle: loads the requested plugin, wires up callbacks,
/// executes extraction/transformation, and coordinates data saving and progress reporting.
/// </summary>
public sealed class IntegrationOrchestrator
{
    private readonly PluginLoader _pluginLoader;
    private readonly IDataSaver _dataSaver;
    private readonly IProgressReporter _progressReporter;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<IntegrationOrchestrator> _logger;

    public IntegrationOrchestrator(
        PluginLoader pluginLoader,
        IDataSaver dataSaver,
        IProgressReporter progressReporter,
        ILoggerFactory loggerFactory,
        ILogger<IntegrationOrchestrator> logger)
    {
        _pluginLoader = pluginLoader;
        _dataSaver = dataSaver;
        _progressReporter = progressReporter;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public async Task ProcessAsync(IntegrationRequestMessage message, CancellationToken cancellationToken)
    {
        var job = new IntegrationJob(message.RequestId, message.PluginName);

        _logger.LogInformation("Processing request {RequestId} with plugin {PluginName}",
            message.RequestId, message.PluginName);

        IIntegrationPlugin plugin;
        try
        {
            plugin = _pluginLoader.LoadPlugin(message.PluginName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin {PluginName} for request {RequestId}",
                message.PluginName, message.RequestId);
            job.MarkFailed($"Plugin load failed: {ex.Message}");
            await _progressReporter.ReportFailedAsync(message.RequestId, ex.Message, ex);
            return;
        }

        var context = BuildPluginContext(message, job);

        try
        {
            await plugin.InitializeAsync(context);
            job.MarkStarted($"Plugin {message.PluginName} initialized");
            await _progressReporter.ReportStartedAsync(message.RequestId, message.PluginName,
                $"Plugin {plugin.Name} initialized successfully");

            await plugin.ExecuteAsync(cancellationToken);

            job.MarkCompleted($"Plugin {message.PluginName} completed successfully");
            await _progressReporter.ReportCompletedAsync(message.RequestId,
                $"Extraction completed. {job.ProgressEntries.Count} progress entries recorded.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Request {RequestId} was cancelled", message.RequestId);
            job.MarkFailed("Operation cancelled");
            await _progressReporter.ReportFailedAsync(message.RequestId, "Operation cancelled", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin execution failed for request {RequestId}", message.RequestId);
            job.MarkFailed(ex.Message);
            await _progressReporter.ReportFailedAsync(message.RequestId, ex.Message, ex);
        }
        finally
        {
            try
            {
                await plugin.ShutdownAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin shutdown failed for request {RequestId}", message.RequestId);
            }
        }
    }

    private PluginContext BuildPluginContext(IntegrationRequestMessage message, IntegrationJob job)
    {
        return new PluginContext
        {
            RequestId = message.RequestId,
            Credentials = new PluginCredentials
            {
                Username = message.Credentials.Username,
                Password = message.Credentials.Password,
                AdditionalFields = message.Credentials.AdditionalFields
            },
            Configuration = message.Configuration,
            Logger = _loggerFactory.CreateLogger($"Plugin.{message.PluginName}"),
            OnStarted = async description =>
            {
                _logger.LogInformation("[{RequestId}] Started: {Description}", message.RequestId, description);
                job.MarkStarted(description);
                await _progressReporter.ReportStartedAsync(message.RequestId, message.PluginName, description);
            },
            OnProgress = async (current, total, description) =>
            {
                _logger.LogInformation("[{RequestId}] Progress: {Current}/{Total} - {Description}",
                    message.RequestId, current, total, description);
                job.RecordProgress(current, total, description);
                await _progressReporter.ReportProgressAsync(message.RequestId, current, total, description);
            },
            OnDataReady = async (dataType, dataStream) =>
            {
                _logger.LogInformation("[{RequestId}] Data ready: {DataType}", message.RequestId, dataType);
                await _dataSaver.SaveAsync(message.RequestId, dataType, dataStream, default);
            },
            OnFailed = async (errorMessage, exception) =>
            {
                _logger.LogError(exception, "[{RequestId}] Failed: {ErrorMessage}",
                    message.RequestId, errorMessage);
                job.MarkFailed(errorMessage);
                await _progressReporter.ReportFailedAsync(message.RequestId, errorMessage, exception);
            },
            OnCompleted = async summary =>
            {
                _logger.LogInformation("[{RequestId}] Completed: {Summary}", message.RequestId, summary);
                job.MarkCompleted(summary);
                await _progressReporter.ReportCompletedAsync(message.RequestId, summary);
            }
        };
    }
}

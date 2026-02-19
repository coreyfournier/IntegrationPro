using Microsoft.Extensions.Logging;

namespace IntegrationPro.PluginBase;

/// <summary>
/// Context provided to plugins during initialization containing credentials,
/// configuration, and callback delegates for reporting progress and saving data.
/// </summary>
public sealed class PluginContext
{
    /// <summary>
    /// Unique identifier for this integration request.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Credentials supplied from the Service Bus message for the plugin to authenticate
    /// with the target system (e.g., username, password, API keys).
    /// </summary>
    public required PluginCredentials Credentials { get; init; }

    /// <summary>
    /// Plugin-specific configuration key/value pairs from the Service Bus message.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Configuration { get; init; }

    /// <summary>
    /// Logger instance for the plugin to use.
    /// </summary>
    public required ILogger Logger { get; init; }

    /// <summary>
    /// Callback invoked when extraction starts. Parameter is a description message.
    /// </summary>
    public required Func<string, Task> OnStarted { get; init; }

    /// <summary>
    /// Callback invoked to report progress. Parameters: (currentStep, totalSteps, description).
    /// </summary>
    public required Func<int, int, string, Task> OnProgress { get; init; }

    /// <summary>
    /// Callback invoked when data is ready to be saved.
    /// Passes a generic data stream — the plugin does not dictate the storage model.
    /// Parameters: (dataType, dataStream).
    /// </summary>
    public required Func<string, Stream, Task> OnDataReady { get; init; }

    /// <summary>
    /// Callback invoked when the plugin encounters a failure.
    /// Parameters: (errorMessage, exception).
    /// </summary>
    public required Func<string, Exception?, Task> OnFailed { get; init; }

    /// <summary>
    /// Callback invoked when the plugin completes successfully. Parameter is a summary message.
    /// </summary>
    public required Func<string, Task> OnCompleted { get; init; }
}

using Microsoft.Extensions.Logging;

namespace IntegrationPro.PluginBase;

/// <summary>
/// Core plugin interface that all ETL plugins must implement.
/// Plugins are loaded dynamically using .NET's native plugin architecture (AssemblyLoadContext).
/// </summary>
public interface IIntegrationPlugin
{
    /// <summary>
    /// Unique name identifying this plugin (e.g., "PrismHR", "Mock").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of the plugin.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Initializes the plugin with credentials, configuration, and callback handlers.
    /// This is the first method called before extraction begins.
    /// </summary>
    Task InitializeAsync(PluginContext context);

    /// <summary>
    /// Performs the extraction and transformation phase.
    /// The plugin should call the callbacks on the context to report progress and deliver data.
    /// </summary>
    Task ExecuteAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Called to clean up resources (e.g., logout, close connections).
    /// </summary>
    Task ShutdownAsync();
}

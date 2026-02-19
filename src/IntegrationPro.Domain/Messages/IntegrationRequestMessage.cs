namespace IntegrationPro.Domain.Messages;

/// <summary>
/// Represents the Service Bus message that triggers an integration job.
/// </summary>
public sealed class IntegrationRequestMessage
{
    /// <summary>
    /// Unique identifier for this request.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Name of the plugin to load and execute (e.g., "PrismHR", "Mock").
    /// </summary>
    public required string PluginName { get; init; }

    /// <summary>
    /// Credentials for the plugin to authenticate with the target system.
    /// </summary>
    public required MessageCredentials Credentials { get; init; }

    /// <summary>
    /// Plugin-specific configuration key/value pairs.
    /// </summary>
    public IReadOnlyDictionary<string, string> Configuration { get; init; }
        = new Dictionary<string, string>();
}

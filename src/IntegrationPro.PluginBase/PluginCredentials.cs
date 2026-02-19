namespace IntegrationPro.PluginBase;

/// <summary>
/// Credentials provided to a plugin for authenticating with the target system.
/// </summary>
public sealed class PluginCredentials
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Additional credential fields (e.g., PEO ID, API key, tenant ID).
    /// </summary>
    public IReadOnlyDictionary<string, string> AdditionalFields { get; init; }
        = new Dictionary<string, string>();
}

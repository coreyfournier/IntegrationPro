using Microsoft.Extensions.Logging;

namespace IntegrationPro.PluginBase;

public interface IIntegrationPlugin
{
    string Name { get; }
    string Description { get; }

    /// <summary>Semantic version of this plugin build, e.g. "1.0.0".</summary>
    string Version { get; }

    /// <summary>POCO type describing plugin configuration for schema generation.</summary>
    Type ConfigType { get; }

    /// <summary>POCO type describing required credentials for schema generation.</summary>
    Type CredentialsType { get; }

    Task InitializeAsync(PluginContext context);
    Task ExecuteAsync(CancellationToken cancellationToken);
    Task ShutdownAsync();
}

using IntegrationPro.PluginBase;

namespace IntegrationPro.Application.Catalog;

public interface IPluginCatalog
{
    IReadOnlyList<PluginSummary> ListPlugins();
    IReadOnlyList<string> ListVersions(string pluginName);

    Task<PluginSchema> GetSchemaAsync(string pluginName, string version, CancellationToken cancellationToken = default);

    /// <summary>Resolve a plugin instance. Null version => latest.</summary>
    Task<IIntegrationPlugin> ResolveAsync(string pluginName, string? version, CancellationToken cancellationToken = default);

    /// <summary>Re-scan the plugin source and atomically swap the catalog index. Returns the new plugin count.</summary>
    Task<int> RefreshAsync(CancellationToken cancellationToken = default);
}

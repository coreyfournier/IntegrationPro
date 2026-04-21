using IntegrationPro.PluginBase;

namespace IntegrationPro.Application.Catalog;

public interface IPluginCatalog
{
    IReadOnlyList<PluginSummary> ListPlugins();
    IReadOnlyList<string> ListVersions(string pluginName);
    PluginSchema GetSchema(string pluginName, string version);

    /// <summary>Resolve a plugin instance. Null version => latest.</summary>
    IIntegrationPlugin Resolve(string pluginName, string? version);
}

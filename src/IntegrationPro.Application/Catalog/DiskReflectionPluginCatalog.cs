using System.Collections.Concurrent;
using IntegrationPro.Application.PluginLoading;
using IntegrationPro.PluginBase;
using Microsoft.Extensions.Logging;
using NJsonSchema;
using NJsonSchema.Generation;

namespace IntegrationPro.Application.Catalog;

public sealed class DiskReflectionPluginCatalog : IPluginCatalog
{
    private readonly PluginLoader _loader;
    private readonly ILogger<DiskReflectionPluginCatalog> _logger;

    private readonly ConcurrentDictionary<string, SortedDictionary<Version, Entry>> _index = new();

    public DiskReflectionPluginCatalog(PluginLoader loader, ILogger<DiskReflectionPluginCatalog> logger)
    {
        _loader = loader;
        _logger = logger;
    }

    public Task InitializeAsync()
    {
        foreach (var pluginDir in _loader.ListPluginDirectories())
        {
            foreach (var version in _loader.ListVersions(pluginDir))
            {
                try
                {
                    var plugin = _loader.LoadPlugin(pluginDir, version);
                    var entry = new Entry(
                        FriendlyName: plugin.Name,
                        PluginDirName: pluginDir,
                        Version: version,
                        Description: plugin.Description,
                        Config: JsonSchema.FromType(plugin.ConfigType, SchemaSettings()),
                        Credentials: JsonSchema.FromType(plugin.CredentialsType, SchemaSettings()),
                        Instance: plugin);

                    var versions = _index.GetOrAdd(plugin.Name, _ =>
                        new SortedDictionary<Version, Entry>(Comparer<Version>.Create((a, b) => b.CompareTo(a))));
                    versions[Version.Parse(version)] = entry;

                    _logger.LogInformation("Cataloged plugin {Name} {Version}", plugin.Name, version);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load plugin from {Dir}/{Version}", pluginDir, version);
                }
            }
        }
        return Task.CompletedTask;
    }

    public IReadOnlyList<PluginSummary> ListPlugins() =>
        _index.Select(kvp =>
        {
            var latest = kvp.Value.First().Value;
            return new PluginSummary(kvp.Key, latest.Version, latest.Description);
        }).OrderBy(s => s.Name).ToList();

    public IReadOnlyList<string> ListVersions(string pluginName) =>
        _index.TryGetValue(pluginName, out var versions)
            ? versions.Keys.Select(v => v.ToString()).ToList()
            : Array.Empty<string>();

    public Task<PluginSchema> GetSchemaAsync(string pluginName, string version, CancellationToken cancellationToken = default)
    {
        var entry = Find(pluginName, version);
        return Task.FromResult(new PluginSchema(entry.FriendlyName, entry.Version, entry.Description, entry.Config, entry.Credentials));
    }

    public Task<IIntegrationPlugin> ResolveAsync(string pluginName, string? version, CancellationToken cancellationToken = default)
    {
        var entry = version is null ? FindLatest(pluginName) : Find(pluginName, version);
        return Task.FromResult(entry.Instance);
    }

    private Entry FindLatest(string pluginName)
    {
        if (!_index.TryGetValue(pluginName, out var versions) || versions.Count == 0)
            throw new KeyNotFoundException($"Plugin '{pluginName}' not found.");
        return versions.First().Value;
    }

    private Entry Find(string pluginName, string version)
    {
        if (!_index.TryGetValue(pluginName, out var versions))
            throw new KeyNotFoundException($"Plugin '{pluginName}' not found.");
        if (!versions.TryGetValue(Version.Parse(version), out var entry))
            throw new KeyNotFoundException($"Plugin '{pluginName}' version '{version}' not found.");
        return entry;
    }

    private static SystemTextJsonSchemaGeneratorSettings SchemaSettings() => new()
    {
        SchemaType = SchemaType.JsonSchema,
        DefaultReferenceTypeNullHandling = ReferenceTypeNullHandling.NotNull,
        SerializerOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        },
    };

    private sealed record Entry(
        string FriendlyName,
        string PluginDirName,
        string Version,
        string Description,
        JsonSchema Config,
        JsonSchema Credentials,
        IIntegrationPlugin Instance);
}

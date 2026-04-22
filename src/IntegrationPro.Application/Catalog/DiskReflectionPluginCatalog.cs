using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using IntegrationPro.Application.PluginLoading;
using IntegrationPro.PluginBase;
using Microsoft.Extensions.Logging;

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
                        Config: PocoSchemaBuilder.Build(plugin.ConfigType),
                        Credentials: PocoSchemaBuilder.Build(plugin.CredentialsType),
                        PluginType: plugin.GetType());

                    var versions = _index.GetOrAdd(plugin.Name, _ =>
                        new SortedDictionary<Version, Entry>(Comparer<Version>.Create((a, b) => b.CompareTo(a))));

                    var parsedVersion = Version.Parse(version);
                    if (versions.TryGetValue(parsedVersion, out var existing) && existing.PluginDirName != pluginDir)
                    {
                        _logger.LogError(
                            "Plugin friendly name collision: both '{ExistingDir}' and '{NewDir}' declare Name='{Name}' Version='{Version}'. Keeping '{ExistingDir}', skipping '{NewDir}'.",
                            existing.PluginDirName, pluginDir, plugin.Name, version, existing.PluginDirName, pluginDir);
                        continue;
                    }
                    versions[parsedVersion] = entry;

                    _logger.LogInformation("Cataloged plugin {Name} {Version}", plugin.Name, version);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load plugin from {Dir}/{Version}", pluginDir, version);
                }
            }
        }

        if (_index.IsEmpty)
        {
            _logger.LogWarning(
                "Plugin catalog initialized with 0 plugins. Check that the plugins directory contains valid {{PluginName}}/{{Version}}/ subdirectories.");
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
        // Clone so callers can't mutate the cached schema objects.
        return Task.FromResult(new PluginSchema(
            entry.FriendlyName, entry.Version, entry.Description,
            (JsonObject)entry.Config.DeepClone(),
            (JsonObject)entry.Credentials.DeepClone()));
    }

    /// <summary>
    /// Resolve a plugin instance. Null version => latest.
    /// Returns a fresh instance per call — plugins may hold per-request state on instance fields,
    /// so callers get an isolated instance for their request lifecycle.
    /// </summary>
    public Task<IIntegrationPlugin> ResolveAsync(string pluginName, string? version, CancellationToken ct = default)
    {
        var entry = version is null ? FindLatest(pluginName) : Find(pluginName, version);
        var instance = (IIntegrationPlugin)Activator.CreateInstance(entry.PluginType)!;
        return Task.FromResult(instance);
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

    private sealed record Entry(
        string FriendlyName,
        string PluginDirName,
        string Version,
        string Description,
        JsonObject Config,
        JsonObject Credentials,
        Type PluginType);
}

using System.Reflection;
using IntegrationPro.PluginBase;
using Microsoft.Extensions.Logging;

namespace IntegrationPro.Application.PluginLoading;

/// <summary>
/// Loads IIntegrationPlugin implementations from plugin assemblies at runtime
/// using .NET's native plugin architecture (AssemblyLoadContext + AssemblyDependencyResolver).
/// </summary>
public sealed class PluginLoader
{
    private readonly string _pluginsDirectory;
    private readonly ILogger<PluginLoader> _logger;

    public PluginLoader(string pluginsDirectory, ILogger<PluginLoader> logger)
    {
        _pluginsDirectory = pluginsDirectory;
        _logger = logger;
    }

    /// <summary>
    /// Loads a plugin by name and optional version. Looks for
    /// {pluginName}/{version}/{pluginName}.dll in the plugins directory.
    /// Null version means the highest semver subdir.
    /// </summary>
    public IIntegrationPlugin LoadPlugin(string pluginName, string? version = null)
    {
        var pluginDir = Path.Combine(_pluginsDirectory, pluginName);
        if (!Directory.Exists(pluginDir))
        {
            throw new DirectoryNotFoundException(
                $"Plugin directory not found: '{pluginDir}'. Expected layout: '{_pluginsDirectory}/{{pluginName}}/{{version}}/{{pluginName}}.dll'.");
        }

        var resolvedVersion = version ?? ResolveLatestVersion(pluginDir);
        var pluginDll = Path.Combine(pluginDir, resolvedVersion, $"{pluginName}.dll");

        if (!File.Exists(pluginDll))
        {
            throw new FileNotFoundException(
                $"Plugin assembly not found at '{pluginDll}'. Ensure the plugin is published to '{pluginDir}/{resolvedVersion}/'.");
        }

        _logger.LogInformation("Loading plugin from {PluginPath}", pluginDll);
        var loadContext = new PluginLoadContext(pluginDll);
        var assembly = loadContext.LoadFromAssemblyName(
            new AssemblyName(Path.GetFileNameWithoutExtension(pluginDll)));
        return CreatePlugin(assembly);
    }

    public IReadOnlyList<string> ListVersions(string pluginName)
    {
        var pluginDir = Path.Combine(_pluginsDirectory, pluginName);
        if (!Directory.Exists(pluginDir)) return Array.Empty<string>();
        return Directory.EnumerateDirectories(pluginDir)
            .Select(p => Path.GetFileName(p)!)
            .Where(n => !string.IsNullOrEmpty(n))
            .OrderByDescending(ParseVersion)
            .ToList();
    }

    public IReadOnlyList<string> ListPluginDirectories()
    {
        if (!Directory.Exists(_pluginsDirectory)) return Array.Empty<string>();
        return Directory.EnumerateDirectories(_pluginsDirectory)
            .Select(p => Path.GetFileName(p)!)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();
    }

    private static Version ParseVersion(string s) =>
        Version.TryParse(s, out var v) ? v : new Version(0, 0, 0);

    private string ResolveLatestVersion(string pluginDir)
    {
        var versions = Directory.EnumerateDirectories(pluginDir)
            .Select(p => Path.GetFileName(p)!)
            .Where(n => !string.IsNullOrEmpty(n))
            .OrderByDescending(ParseVersion)
            .ToList();
        if (versions.Count == 0)
            throw new DirectoryNotFoundException(
                $"No version subdirectories found under '{pluginDir}'. Expected '{pluginDir}/{{version}}/{{pluginName}}.dll'.");
        return versions[0];
    }

    private static IIntegrationPlugin CreatePlugin(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (typeof(IIntegrationPlugin).IsAssignableFrom(type) && !type.IsAbstract)
            {
                if (Activator.CreateInstance(type) is IIntegrationPlugin plugin)
                {
                    return plugin;
                }
            }
        }

        var availableTypes = string.Join(", ", assembly.GetTypes().Select(t => t.FullName));
        throw new InvalidOperationException(
            $"No type implementing IIntegrationPlugin found in {assembly.FullName}. " +
            $"Available types: {availableTypes}");
    }
}

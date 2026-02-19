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
    /// Loads a plugin by name. Looks for {pluginName}/{pluginName}.dll in the plugins directory.
    /// </summary>
    public IIntegrationPlugin LoadPlugin(string pluginName)
    {
        var pluginDll = Path.Combine(_pluginsDirectory, pluginName, $"{pluginName}.dll");

        if (!File.Exists(pluginDll))
        {
            throw new FileNotFoundException(
                $"Plugin assembly not found at '{pluginDll}'. " +
                $"Ensure the plugin is published to the plugins directory.");
        }

        _logger.LogInformation("Loading plugin from {PluginPath}", pluginDll);

        var loadContext = new PluginLoadContext(pluginDll);
        var assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(pluginDll));
        var assembly = loadContext.LoadFromAssemblyName(assemblyName);

        return CreatePlugin(assembly);
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

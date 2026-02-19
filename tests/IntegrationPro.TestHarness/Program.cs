using IntegrationPro.Application.PluginLoading;
using IntegrationPro.PluginBase;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<Program>();

// Resolve the published plugins directory (passed as arg or default relative path)
var pluginsDir = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "plugins-output"));

logger.LogInformation("Plugins directory: {PluginsDir}", pluginsDir);

if (!Directory.Exists(pluginsDir))
{
    logger.LogError("Plugins directory not found: {PluginsDir}", pluginsDir);
    return 1;
}

// Load the mock plugin
var pluginLoader = new PluginLoader(pluginsDir, loggerFactory.CreateLogger<PluginLoader>());
var plugin = pluginLoader.LoadPlugin("IntegrationPro.Plugin.Mock");

logger.LogInformation("Loaded plugin: {Name} - {Description}", plugin.Name, plugin.Description);

// Track data received
var dataReceived = new List<(string DataType, long ByteCount)>();

var context = new PluginContext
{
    RequestId = Guid.NewGuid().ToString("N")[..8],
    Credentials = new PluginCredentials
    {
        Username = "test-user",
        Password = "test-pass"
    },
    Configuration = new Dictionary<string, string>
    {
        ["CompanyCount"] = "5",
        ["DelayMs"] = "50",
        ["SimulateFailure"] = "false"
    },
    Logger = loggerFactory.CreateLogger("Plugin.Mock"),
    OnStarted = msg =>
    {
        logger.LogInformation("[CALLBACK] Started: {Message}", msg);
        return Task.CompletedTask;
    },
    OnProgress = (current, total, desc) =>
    {
        logger.LogInformation("[CALLBACK] Progress: {Current}/{Total} - {Description}", current, total, desc);
        return Task.CompletedTask;
    },
    OnDataReady = (dataType, stream) =>
    {
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        dataReceived.Add((dataType, content.Length));
        logger.LogInformation("[CALLBACK] DataReady: type={DataType}, size={Size} bytes", dataType, content.Length);
        // Print a preview of the data
        var preview = content.Length > 500 ? content[..500] + "..." : content;
        logger.LogInformation("[CALLBACK] Data preview:\n{Preview}", preview);
        return Task.CompletedTask;
    },
    OnFailed = (msg, ex) =>
    {
        logger.LogError(ex, "[CALLBACK] Failed: {Message}", msg);
        return Task.CompletedTask;
    },
    OnCompleted = msg =>
    {
        logger.LogInformation("[CALLBACK] Completed: {Message}", msg);
        return Task.CompletedTask;
    }
};

// Run the plugin
logger.LogInformation("=== Initializing Mock plugin ===");
await plugin.InitializeAsync(context);

logger.LogInformation("=== Executing Mock plugin ===");
await plugin.ExecuteAsync(CancellationToken.None);

logger.LogInformation("=== Shutting down Mock plugin ===");
await plugin.ShutdownAsync();

// Summary
logger.LogInformation("=== Test Complete ===");
logger.LogInformation("Data batches received: {Count}", dataReceived.Count);
foreach (var (dataType, byteCount) in dataReceived)
{
    logger.LogInformation("  - {DataType}: {ByteCount} bytes", dataType, byteCount);
}

return 0;

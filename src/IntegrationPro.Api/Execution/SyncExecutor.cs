using IntegrationPro.Api.Contracts;
using IntegrationPro.Application.Catalog;
using IntegrationPro.Application.Interfaces;
using IntegrationPro.Domain.Messages;
using Microsoft.Extensions.Logging;

namespace IntegrationPro.Api.Execution;

public sealed class SyncExecutor
{
    private readonly IPluginCatalog _catalog;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IProgressReporter _progressReporter;
    private readonly ILogger<SyncExecutor> _logger;

    public SyncExecutor(IPluginCatalog catalog, ILoggerFactory loggerFactory,
                        IProgressReporter progressReporter, ILogger<SyncExecutor> logger)
    {
        _catalog = catalog;
        _loggerFactory = loggerFactory;
        _progressReporter = progressReporter;
        _logger = logger;
    }

    public async Task<SyncResult> ExecuteAsync(
        RunIntegrationRequest request, HttpContext http, CancellationToken ct)
    {
        var requestId = Guid.NewGuid().ToString("N");

        PluginSchema schema;
        try
        {
            var effectiveVersion = request.Version ?? FirstVersion(request.PluginName);
            schema = await _catalog.GetSchemaAsync(request.PluginName, effectiveVersion, ct);
        }
        catch (KeyNotFoundException)
        {
            return SyncResult.NotFound(requestId, request);
        }

        var credErrors = schema.Credentials.Validate(request.Credentials.ToJsonString());
        var cfgErrors  = schema.Config.Validate(request.Configuration.ToJsonString());
        if (credErrors.Count > 0 || cfgErrors.Count > 0)
            return SyncResult.Validation(requestId, request,
                credErrors.Select(e => "credentials: " + e).Concat(cfgErrors.Select(e => "configuration: " + e)).ToList());

        // We bypass IntegrationOrchestrator here because sync mode uses a different
        // IDataSaver (SyncDataSaver buffering into HttpResponse) and different progress
        // semantics than the ServiceBus path.
        var plugin = await _catalog.ResolveAsync(request.PluginName, request.Version, ct);
        var message = BuildMessage(requestId, plugin.Name, plugin.Version, request);
        var saver = new SyncDataSaver();
        var context = SyncContextBuilder.Build(plugin, message, saver, _progressReporter, _loggerFactory, ct);

        try
        {
            await plugin.InitializeAsync(context);
            await plugin.ExecuteAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return SyncResult.Timeout(requestId, request);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Sync mode allows"))
        {
            return SyncResult.MultiEmission(requestId, request, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin execution failed for {RequestId}", requestId);
            return SyncResult.Failed(requestId, request, ex.Message);
        }
        finally
        {
            try { await plugin.ShutdownAsync(); } catch (Exception ex)
            { _logger.LogWarning(ex, "Shutdown failed for {RequestId}", requestId); }
        }

        return SyncResult.Ok(requestId, request, plugin.Version, saver);
    }

    private string FirstVersion(string name) =>
        _catalog.ListVersions(name).FirstOrDefault()
            ?? throw new KeyNotFoundException($"Plugin '{name}' not found.");

    private static IntegrationRequestMessage BuildMessage(string requestId, string name, string version, RunIntegrationRequest r)
    {
        var credUsername = r.Credentials["username"]?.GetValue<string>() ?? "";
        var credPassword = r.Credentials["password"]?.GetValue<string>() ?? "";
        var additional = r.Credentials
            .Where(kv => kv.Key is not ("username" or "password"))
            .ToDictionary(kv => UpperFirst(kv.Key), kv => kv.Value?.ToString() ?? "");

        var cfg = r.Configuration.ToDictionary(kv => UpperFirst(kv.Key), kv => kv.Value?.ToString() ?? "");

        return new IntegrationRequestMessage
        {
            RequestId = requestId,
            PluginName = name,
            Credentials = new MessageCredentials
            {
                Username = credUsername, Password = credPassword,
                AdditionalFields = additional
            },
            Configuration = cfg
        };
    }

    private static string UpperFirst(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}

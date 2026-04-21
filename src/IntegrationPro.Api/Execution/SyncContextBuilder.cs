using IntegrationPro.Application.Interfaces;
using IntegrationPro.Domain.Messages;
using IntegrationPro.PluginBase;
using Microsoft.Extensions.Logging;

namespace IntegrationPro.Api.Execution;

internal static class SyncContextBuilder
{
    public static PluginContext Build(
        IIntegrationPlugin plugin,
        IntegrationRequestMessage msg,
        SyncDataSaver saver,
        IProgressReporter progress,
        ILoggerFactory loggerFactory,
        CancellationToken ct) => new()
    {
        RequestId = msg.RequestId,
        Credentials = new PluginCredentials
        {
            Username = msg.Credentials.Username, Password = msg.Credentials.Password,
            AdditionalFields = msg.Credentials.AdditionalFields
        },
        Configuration = msg.Configuration,
        Logger = loggerFactory.CreateLogger($"Plugin.{plugin.Name}"),
        OnStarted   = d => progress.ReportStartedAsync(msg.RequestId, plugin.Name, d),
        OnProgress  = (c, t, d) => progress.ReportProgressAsync(msg.RequestId, c, t, d),
        OnDataReady = (type, stream) => saver.SaveAsync(msg.RequestId, type, stream, ct),
        OnFailed    = (m, ex) => progress.ReportFailedAsync(msg.RequestId, m, ex),
        OnCompleted = s => progress.ReportCompletedAsync(msg.RequestId, s),
    };
}

using IntegrationPro.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IntegrationPro.Infrastructure.HealthChecks;

public sealed class LivenessHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy("Process is alive."));
    }
}

public sealed class ReadinessHealthCheck : IHealthCheck
{
    private const int ConsecutiveFailureThreshold = 3;
    private readonly IJobStatusStore _statusStore;

    public ReadinessHealthCheck(IJobStatusStore statusStore)
    {
        _statusStore = statusStore;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_statusStore.HasReceivedAnyJob)
        {
            return Task.FromResult(HealthCheckResult.Healthy("No jobs processed yet."));
        }

        var recent = _statusStore.GetRecentStatuses();
        // Get the terminal statuses (Completed or Failed), ignoring Running
        var terminalStatuses = recent
            .Where(s => s.Status is JobStatusCategory.Completed or JobStatusCategory.Failed)
            .TakeLast(ConsecutiveFailureThreshold)
            .ToList();

        if (terminalStatuses.Count >= ConsecutiveFailureThreshold &&
            terminalStatuses.All(s => s.Status == JobStatusCategory.Failed))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Last {ConsecutiveFailureThreshold} jobs failed consecutively. " +
                $"Most recent: {terminalStatuses[^1].Description}"));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Latest status: {_statusStore.GetLatestStatus()?.Status.ToString() ?? "Unknown"}"));
    }
}

public sealed class PluginCatalogHealthCheck : IHealthCheck
{
    private readonly Application.Catalog.IPluginCatalog _catalog;

    public PluginCatalogHealthCheck(Application.Catalog.IPluginCatalog catalog) => _catalog = catalog;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var count = _catalog.ListPlugins().Count;
        return Task.FromResult(count > 0
            ? HealthCheckResult.Healthy($"Plugin catalog has {count} plugin(s).")
            : HealthCheckResult.Unhealthy("Plugin catalog is empty — no plugins loaded."));
    }
}

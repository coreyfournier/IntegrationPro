using IntegrationPro.Application.Catalog;
using IntegrationPro.Application.Interfaces;
using IntegrationPro.Application.PluginLoading;
using IntegrationPro.Application.Services;
using IntegrationPro.Infrastructure.DataSaving;
using IntegrationPro.Infrastructure.HealthChecks;
using IntegrationPro.Infrastructure.Progress;
using IntegrationPro.Infrastructure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntegrationPro.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIntegrationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Service Bus
        services.Configure<ServiceBusOptions>(configuration.GetSection(ServiceBusOptions.SectionName));

        // Plugin loader
        var pluginsDir = configuration.GetValue<string>("Plugins:Directory") ?? "/app/plugins";
        services.AddSingleton(sp => new PluginLoader(
            pluginsDir,
            sp.GetRequiredService<ILogger<PluginLoader>>()));

        // Plugin catalog
        // TODO: replace with NuGetFeedPluginCatalog (lazy pull from feed) when feed-backed
        // resolution is ready. Today's disk+reflection impl is fine for the ~dozens-of-plugins scale.
        // Blocks during DI construction — acceptable today because disk+reflection initialize is
        // fully synchronous. The future NuGet-feed impl will need an IHostedService / startup filter
        // to pre-warm the catalog asynchronously.
        services.AddSingleton<IPluginCatalog>(sp =>
        {
            var catalog = new DiskReflectionPluginCatalog(
                sp.GetRequiredService<PluginLoader>(),
                sp.GetRequiredService<ILogger<DiskReflectionPluginCatalog>>());
            catalog.InitializeAsync().GetAwaiter().GetResult();
            return catalog;
        });

        // Data saver
        var outputDir = configuration.GetValue<string>("DataOutput:Directory") ?? "/app/output";
        services.AddSingleton<IDataSaver>(sp => new FileSystemDataSaver(
            outputDir,
            sp.GetRequiredService<ILogger<FileSystemDataSaver>>()));

        // Job status store (singleton, shared between decorator and health checks)
        services.AddSingleton<IJobStatusStore, JobStatusStore>();

        // Progress reporter: LoggingProgressReporter wrapped by HealthTrackingProgressReporter
        services.AddSingleton<LoggingProgressReporter>();
        services.AddSingleton<IProgressReporter>(sp =>
            new HealthTrackingProgressReporter(
                sp.GetRequiredService<LoggingProgressReporter>(),
                sp.GetRequiredService<IJobStatusStore>()));

        // Health checks
        services.AddHealthChecks()
            .AddCheck<LivenessHealthCheck>("liveness", tags: new[] { "live" })
            .AddCheck<ReadinessHealthCheck>("readiness", tags: new[] { "ready" });

        // Orchestrator
        services.AddSingleton<IntegrationOrchestrator>();

        // Service Bus consumer (hosted service)
        services.AddHostedService<ServiceBusConsumer>();

        return services;
    }
}

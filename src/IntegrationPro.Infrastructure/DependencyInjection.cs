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
        this IServiceCollection services, IConfiguration configuration)
    {
        AddShared(services, configuration);
        services.AddHostedService<ServiceBusConsumer>();
        return services;
    }

    public static IServiceCollection AddIntegrationInfrastructureForApi(
        this IServiceCollection services, IConfiguration configuration)
    {
        AddShared(services, configuration);
        // No ServiceBusConsumer — Api is HTTP-only.
        return services;
    }

    private static void AddShared(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ServiceBusOptions>(configuration.GetSection(ServiceBusOptions.SectionName));

        var pluginsDir = configuration.GetValue<string>("Plugins:Directory") ?? "/app/plugins";
        services.AddSingleton(sp => new PluginLoader(
            pluginsDir, sp.GetRequiredService<ILogger<PluginLoader>>()));

        var outputDir = configuration.GetValue<string>("DataOutput:Directory") ?? "/app/output";
        services.AddSingleton<IDataSaver>(sp => new FileSystemDataSaver(
            outputDir, sp.GetRequiredService<ILogger<FileSystemDataSaver>>()));

        services.AddSingleton<IJobStatusStore, JobStatusStore>();
        services.AddSingleton<LoggingProgressReporter>();
        services.AddSingleton<IProgressReporter>(sp =>
            new HealthTrackingProgressReporter(
                sp.GetRequiredService<LoggingProgressReporter>(),
                sp.GetRequiredService<IJobStatusStore>()));

        services.AddHealthChecks()
            .AddCheck<LivenessHealthCheck>("liveness", tags: new[] { "live" })
            .AddCheck<ReadinessHealthCheck>("readiness", tags: new[] { "ready" });

        services.AddSingleton<IntegrationOrchestrator>();

        // TODO: replace with NuGetFeedPluginCatalog (lazy pull from feed) for 300+ plugins at scale.
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
    }
}

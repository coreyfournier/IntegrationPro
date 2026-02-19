using IntegrationPro.Application.Interfaces;
using IntegrationPro.Application.PluginLoading;
using IntegrationPro.Application.Services;
using IntegrationPro.Infrastructure.DataSaving;
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

        // Data saver
        var outputDir = configuration.GetValue<string>("DataOutput:Directory") ?? "/app/output";
        services.AddSingleton<IDataSaver>(sp => new FileSystemDataSaver(
            outputDir,
            sp.GetRequiredService<ILogger<FileSystemDataSaver>>()));

        // Progress reporter
        services.AddSingleton<IProgressReporter, LoggingProgressReporter>();

        // Orchestrator
        services.AddSingleton<IntegrationOrchestrator>();

        // Service Bus consumer (hosted service)
        services.AddHostedService<ServiceBusConsumer>();

        return services;
    }
}

using System.Text.Json;
using Azure.Messaging.ServiceBus;
using IntegrationPro.Application.Services;
using IntegrationPro.Domain.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationPro.Infrastructure.ServiceBus;

/// <summary>
/// Background service that listens to Azure Service Bus for integration request messages
/// and dispatches them to the IntegrationOrchestrator.
/// </summary>
public sealed class ServiceBusConsumer : BackgroundService
{
    private readonly ServiceBusOptions _options;
    private readonly IntegrationOrchestrator _orchestrator;
    private readonly ILogger<ServiceBusConsumer> _logger;
    private ServiceBusClient? _client;
    private ServiceBusProcessor? _processor;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ServiceBusConsumer(
        IOptions<ServiceBusOptions> options,
        IntegrationOrchestrator orchestrator,
        ILogger<ServiceBusConsumer> logger)
    {
        _options = options.Value;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Service Bus consumer on queue '{QueueName}'", _options.QueueName);

        _client = new ServiceBusClient(_options.ConnectionString);
        _processor = _client.CreateProcessor(_options.QueueName, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = _options.MaxConcurrentCalls,
            AutoCompleteMessages = false
        });

        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        // Keep running until cancellation is requested
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var body = args.Message.Body.ToString();
        _logger.LogInformation("Received message: {MessageId}", args.Message.MessageId);

        try
        {
            var message = JsonSerializer.Deserialize<IntegrationRequestMessage>(body, JsonOptions);

            if (message == null)
            {
                _logger.LogError("Failed to deserialize message {MessageId}", args.Message.MessageId);
                await args.DeadLetterMessageAsync(args.Message, "Deserialization failed");
                return;
            }

            await _orchestrator.ProcessAsync(message, args.CancellationToken);
            await args.CompleteMessageAsync(args.Message);

            _logger.LogInformation("Successfully processed message {MessageId} for request {RequestId}",
                args.Message.MessageId, message.RequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message {MessageId}", args.Message.MessageId);
            await args.AbandonMessageAsync(args.Message);
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception,
            "Service Bus error. Source: {ErrorSource}, Namespace: {Namespace}, Entity: {EntityPath}",
            args.ErrorSource, args.FullyQualifiedNamespace, args.EntityPath);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Service Bus consumer");

        if (_processor != null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }

        if (_client != null)
        {
            await _client.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}

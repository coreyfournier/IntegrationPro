namespace IntegrationPro.Infrastructure.ServiceBus;

public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    public string ConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = "integration-requests";
    public int MaxConcurrentCalls { get; set; } = 1;
}

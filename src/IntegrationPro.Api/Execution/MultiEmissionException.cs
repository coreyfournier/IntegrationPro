namespace IntegrationPro.Api.Execution;

/// <summary>
/// Thrown when a plugin calls OnDataReady more than once in sync execution mode.
/// Multi-emission plugins must use the Service Bus path.
/// </summary>
internal sealed class MultiEmissionException : InvalidOperationException
{
    public MultiEmissionException()
        : base("Sync mode allows a single OnDataReady emission. Use the Service Bus path for multi-emission plugins.")
    { }
}

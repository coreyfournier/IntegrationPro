namespace IntegrationPro.Domain.Messages;

/// <summary>
/// Credential data carried in the Service Bus message.
/// </summary>
public sealed class MessageCredentials
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> AdditionalFields { get; init; }
        = new Dictionary<string, string>();
}

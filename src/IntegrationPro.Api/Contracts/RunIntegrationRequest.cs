using System.Text.Json.Nodes;

namespace IntegrationPro.Api.Contracts;

public sealed record RunIntegrationRequest(
    string PluginName,
    string? Version,
    int? TimeoutSeconds,
    JsonObject Credentials,
    JsonObject Configuration);

using System.Text.Json.Nodes;

namespace IntegrationPro.Api.Contracts;

public sealed record PluginSchemaResponse(
    string Name, string Version, string Description,
    JsonObject Config, JsonObject Credentials);

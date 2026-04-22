using System.Text.Json.Nodes;

namespace IntegrationPro.Application.Catalog;

public sealed record PluginSchema(
    string Name,
    string Version,
    string Description,
    JsonObject Config,
    JsonObject Credentials);

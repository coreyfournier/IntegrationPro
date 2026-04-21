using NJsonSchema;

namespace IntegrationPro.Application.Catalog;

public sealed record PluginSchema(
    string Name,
    string Version,
    string Description,
    JsonSchema Config,
    JsonSchema Credentials);

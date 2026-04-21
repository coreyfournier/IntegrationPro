namespace IntegrationPro.Api.Contracts;

public sealed record PluginVersionsResponse(string Name, IReadOnlyList<string> Versions);

namespace IntegrationPro.Api.Contracts;

public sealed record ListPluginsResponse(
    IReadOnlyList<PluginSummaryDto> Items, int Total, int Page, int PageSize);

public sealed record PluginSummaryDto(string Name, string LatestVersion, string Description);

namespace IntegrationPro.Api.Contracts;

public sealed record ErrorResponse(
    string RequestId,
    string PluginName,
    string? Version,
    string Status,
    ErrorDetail Error);

public sealed record ErrorDetail(string Code, string Message, IReadOnlyList<string>? Details = null);

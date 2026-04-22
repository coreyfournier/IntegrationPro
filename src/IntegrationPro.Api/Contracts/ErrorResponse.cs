namespace IntegrationPro.Api.Contracts;

public sealed record ErrorResponse(
    string RequestId,
    string PluginName,
    string? Version,
    string Status,
    ErrorDetail Error);

/// <summary>
/// Structured error information. <c>Code</c> is a stable machine-readable discriminator;
/// <c>Category</c>, <c>Retryable</c>, <c>UpstreamStatus</c>, and <c>ExceptionType</c> are
/// populated when the host can classify the failure, and should be used by clients to
/// decide retry vs. escalation.
/// </summary>
public sealed record ErrorDetail(
    string Code,
    string Message,
    IReadOnlyList<string>? Details = null,
    string? Category = null,
    bool? Retryable = null,
    int? UpstreamStatus = null,
    string? ExceptionType = null);

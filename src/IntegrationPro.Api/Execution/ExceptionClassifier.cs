using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using IntegrationPro.Api.Contracts;

namespace IntegrationPro.Api.Execution;

/// <summary>
/// Translates exceptions raised during plugin execution into structured <see cref="ErrorDetail"/>
/// records. Category/retryable are inferred from common .NET exception types so callers can tell
/// a configuration mistake apart from a transient upstream blip without parsing messages.
/// </summary>
internal static class ExceptionClassifier
{
    public const string CategoryConfiguration = "configuration";
    public const string CategoryAuthentication = "authentication";
    public const string CategoryUpstreamHttp = "upstream_http";
    public const string CategoryUpstreamTimeout = "upstream_timeout";
    public const string CategoryUnexpected = "unexpected";

    public static ErrorDetail Classify(Exception ex)
    {
        var type = ex.GetType().Name;
        var msg = ex.Message;

        switch (ex)
        {
            case UriFormatException:
                return new ErrorDetail("plugin_failure", msg,
                    Category: CategoryConfiguration, Retryable: false, ExceptionType: type);

            case HttpRequestException hre:
                var status = hre.StatusCode is null ? (int?)null : (int)hre.StatusCode.Value;
                // Null status = network-level failure (DNS, TCP, TLS handshake) → retryable.
                // 4xx = client error → not retryable without a code/config change.
                // 5xx = server error → retryable.
                var retry = status is null ? true : status >= 500;
                return new ErrorDetail("plugin_failure", msg,
                    Category: CategoryUpstreamHttp,
                    Retryable: retry,
                    UpstreamStatus: status,
                    ExceptionType: type);

            case TaskCanceledException:
                // User-cancellation is handled separately in SyncExecutor's OperationCanceledException
                // filter; anything reaching here is a post-fact HTTP timeout (HttpClient default 100s)
                // or similar upstream-side cancel.
                return new ErrorDetail("plugin_failure", msg,
                    Category: CategoryUpstreamTimeout, Retryable: true, ExceptionType: type);

            case AuthenticationException:
            case UnauthorizedAccessException:
                return new ErrorDetail("plugin_failure", msg,
                    Category: CategoryAuthentication, Retryable: false, ExceptionType: type);

            default:
                return new ErrorDetail("plugin_failure", msg,
                    Category: CategoryUnexpected, Retryable: null, ExceptionType: type);
        }
    }
}

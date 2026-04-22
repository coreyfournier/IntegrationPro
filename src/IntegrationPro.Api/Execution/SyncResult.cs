using IntegrationPro.Api.Contracts;

namespace IntegrationPro.Api.Execution;

public abstract record SyncResult
{
    public sealed record Success(string RequestId, string PluginName, string Version, SyncDataSaver Saver) : SyncResult;
    public sealed record NotFoundErr(ErrorResponse Body) : SyncResult;
    public sealed record ValidationErr(ErrorResponse Body) : SyncResult;
    public sealed record TimeoutErr(ErrorResponse Body) : SyncResult;
    public sealed record MultiEmissionErr(ErrorResponse Body) : SyncResult;
    public sealed record FailedErr(ErrorResponse Body) : SyncResult;

    public static SyncResult Ok(string id, RunIntegrationRequest r, string v, SyncDataSaver s) =>
        new Success(id, r.PluginName, v, s);

    public static SyncResult NotFound(string id, RunIntegrationRequest r) =>
        new NotFoundErr(new ErrorResponse(id, r.PluginName, r.Version, "Failed",
            new ErrorDetail("plugin_not_found",
                $"Plugin '{r.PluginName}' or version '{r.Version}' not found.",
                Category: ExceptionClassifier.CategoryConfiguration,
                Retryable: false)));

    public static SyncResult Validation(string id, RunIntegrationRequest r, IReadOnlyList<string> details) =>
        new ValidationErr(new ErrorResponse(id, r.PluginName, r.Version, "Failed",
            new ErrorDetail("validation_failed",
                "Request body does not match plugin schema.",
                Details: details,
                Category: ExceptionClassifier.CategoryConfiguration,
                Retryable: false)));

    public static SyncResult Timeout(string id, RunIntegrationRequest r) =>
        new TimeoutErr(new ErrorResponse(id, r.PluginName, r.Version, "Failed",
            new ErrorDetail("timeout",
                "Plugin execution exceeded the allowed duration.",
                Category: ExceptionClassifier.CategoryUpstreamTimeout,
                Retryable: true)));

    public static SyncResult MultiEmission(string id, RunIntegrationRequest r, string msg) =>
        new MultiEmissionErr(new ErrorResponse(id, r.PluginName, r.Version, "Failed",
            new ErrorDetail("multi_emission", msg,
                Category: ExceptionClassifier.CategoryConfiguration,
                Retryable: false)));

    /// <summary>Classify the exception and wrap it as a 500-Failed result.</summary>
    public static SyncResult Failed(string id, RunIntegrationRequest r, Exception ex) =>
        new FailedErr(new ErrorResponse(id, r.PluginName, r.Version, "Failed",
            ExceptionClassifier.Classify(ex)));
}

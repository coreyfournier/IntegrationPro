using IntegrationPro.Api.Contracts;
using IntegrationPro.Api.Execution;
using Microsoft.AspNetCore.WebUtilities;

namespace IntegrationPro.Api;

public static class IntegrationEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/integrations/run", async (
            RunIntegrationRequest request,
            SyncExecutor executor,
            HttpContext http,
            IConfiguration config) =>
        {
            var maxSeconds = config.GetValue<int?>("Api:MaxRequestSeconds") ?? 300;
            var requested = request.TimeoutSeconds ?? maxSeconds;
            if (requested <= 0)
            {
                return Results.Json(
                    new ErrorResponse(Guid.NewGuid().ToString("N"), request.PluginName, request.Version, "Failed",
                        new ErrorDetail("validation_failed", "timeoutSeconds must be positive.")),
                    statusCode: StatusCodes.Status400BadRequest);
            }
            var effectiveSeconds = Math.Min(requested, maxSeconds);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
            cts.CancelAfter(TimeSpan.FromSeconds(effectiveSeconds));

            var result = await executor.ExecuteAsync(request, http, cts.Token);

            try
            {
                switch (result)
                {
                    case SyncResult.Success s:
                        http.Response.StatusCode = StatusCodes.Status200OK;
                        http.Response.ContentType = "application/json";
                        http.Response.Headers["X-Request-Id"] = s.RequestId;
                        http.Response.Headers["X-Plugin-Version"] = s.Version;
                        await s.Saver.Buffer.DrainBufferAsync(http.Response.Body, http.RequestAborted);
                        return Results.Empty;
                    case SyncResult.NotFoundErr n:
                        return Results.Json(n.Body, statusCode: StatusCodes.Status404NotFound);
                    case SyncResult.ValidationErr v:
                        return Results.Json(v.Body, statusCode: StatusCodes.Status400BadRequest);
                    case SyncResult.TimeoutErr t:
                        return Results.Json(t.Body, statusCode: StatusCodes.Status408RequestTimeout);
                    case SyncResult.MultiEmissionErr m:
                        return Results.Json(m.Body, statusCode: StatusCodes.Status409Conflict);
                    case SyncResult.FailedErr f:
                        return Results.Json(f.Body, statusCode: StatusCodes.Status500InternalServerError);
                    default:
                        return Results.StatusCode(500);
                }
            }
            finally
            {
                if (result is SyncResult.Success success)
                    await success.Saver.DisposeAsync();
            }
        });
    }
}

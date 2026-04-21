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
            var clientSeconds = request.TimeoutSeconds ?? maxSeconds;
            var effectiveSeconds = Math.Min(clientSeconds, maxSeconds);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
            cts.CancelAfter(TimeSpan.FromSeconds(effectiveSeconds));

            var result = await executor.ExecuteAsync(request, http, cts.Token);

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
        });
    }
}

using Microsoft.AspNetCore.WebUtilities;
using IntegrationPro.Application.Interfaces;

namespace IntegrationPro.Api.Execution;

/// <summary>
/// Captures a single OnDataReady emission into a FileBufferingWriteStream so the
/// outer pipeline can decide between flushing (success) and discarding (error).
/// Throws on a second emission — sync mode is single-emission only.
/// </summary>
public sealed class SyncDataSaver : IDataSaver, IAsyncDisposable
{
    // Buffered to allow proper 4xx/5xx error responses on mid-run failures.
    // Optimization: swap for direct pipe-through (plugin stream -> response.Body)
    // if streaming semantics and lower TTFB become more important than clean
    // error status codes.
    public FileBufferingWriteStream Buffer { get; } = new(memoryThreshold: 30 * 1024);

    public string? DataType { get; private set; }
    public bool HasEmitted => DataType is not null;

    public async Task SaveAsync(string requestId, string dataType, Stream data, CancellationToken ct = default)
    {
        if (HasEmitted)
            throw new MultiEmissionException();
        DataType = dataType;
        await data.CopyToAsync(Buffer, ct);
    }

    public async ValueTask DisposeAsync()
    {
        await Buffer.DisposeAsync();
    }
}

using System.Text.Json;

namespace IntegrationPro.Plugin.PrismHR;

/// <summary>
/// Utility to serialize data to a stream for delivery via the OnDataReady callback.
/// </summary>
internal static class DataSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static MemoryStream SerializeToStream<T>(T data)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, data, Options);
        stream.Position = 0;
        return stream;
    }
}

using IntegrationPro.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace IntegrationPro.Infrastructure.DataSaving;

/// <summary>
/// Saves extracted data to the local file system.
/// In production, replace with blob storage, database, or other destination.
/// </summary>
public sealed class FileSystemDataSaver : IDataSaver
{
    private readonly string _outputDirectory;
    private readonly ILogger<FileSystemDataSaver> _logger;

    public FileSystemDataSaver(string outputDirectory, ILogger<FileSystemDataSaver> logger)
    {
        _outputDirectory = outputDirectory;
        _logger = logger;
    }

    public async Task SaveAsync(string requestId, string dataType, Stream data, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(_outputDirectory, requestId);
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, $"{dataType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");

        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await data.CopyToAsync(fileStream, cancellationToken);

        _logger.LogInformation("Data saved to {FilePath} for request {RequestId}", filePath, requestId);
    }
}

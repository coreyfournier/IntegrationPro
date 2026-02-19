namespace IntegrationPro.Application.Interfaces;

/// <summary>
/// Abstraction for persisting extracted data.
/// The implementation decides the destination (blob storage, database, file system, etc.).
/// </summary>
public interface IDataSaver
{
    /// <summary>
    /// Saves data from the provided stream.
    /// </summary>
    /// <param name="requestId">Unique request identifier for correlation.</param>
    /// <param name="dataType">The type/category of data being saved (e.g., "companies").</param>
    /// <param name="data">Raw data stream from the plugin.</param>
    Task SaveAsync(string requestId, string dataType, Stream data, CancellationToken cancellationToken = default);
}

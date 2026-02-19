using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace IntegrationPro.Plugin.PrismHR;

/// <summary>
/// HTTP client for the PrismHR Services REST API.
/// Handles session-based authentication via LoginService.createPeoSession
/// and data retrieval via ClientMasterService.
/// </summary>
internal sealed class PrismHrApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private string? _sessionId;

    public PrismHrApiClient(string baseUrl, ILogger logger)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/prismhr-api/")
        };
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _logger = logger;
    }

    /// <summary>
    /// Authenticates with PrismHR and obtains a session token.
    /// Uses LoginService.createPeoSession with username, password, and PEO ID.
    /// </summary>
    public async Task LoginAsync(string username, string password, string peoId)
    {
        var requestBody = new
        {
            username,
            password,
            peoId
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("api/LoginService/createPeoSession", content);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseBody);

        _sessionId = doc.RootElement.TryGetProperty("sessionId", out var sessionProp)
            ? sessionProp.GetString()
            : throw new InvalidOperationException("Login response did not contain a sessionId");

        _httpClient.DefaultRequestHeaders.Add("X-Session-Id", _sessionId);

        _logger.LogInformation("PrismHR session established: {SessionId}", _sessionId?[..8] + "...");
    }

    /// <summary>
    /// Retrieves the list of clients/companies from PrismHR.
    /// Uses ClientMasterService.getClientList.
    /// </summary>
    public async Task<string> GetClientListAsync(CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await _httpClient.GetAsync(
            "api/ClientMasterService/getClientList",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// Terminates the PrismHR session using LoginService.invalidateSession.
    /// </summary>
    public async Task LogoutAsync()
    {
        if (_sessionId == null) return;

        try
        {
            await _httpClient.PostAsync("api/LoginService/invalidateSession", null);
            _logger.LogInformation("PrismHR session invalidated");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate PrismHR session");
        }
    }

    private void EnsureAuthenticated()
    {
        if (string.IsNullOrEmpty(_sessionId))
        {
            throw new InvalidOperationException(
                "Not authenticated. Call LoginAsync before making API requests.");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

using IntegrationPro.PluginBase;
using Microsoft.Extensions.Logging;

namespace IntegrationPro.Plugin.PrismHR;

/// <summary>
/// Plugin that extracts company information from the PrismHR API.
/// Uses the PrismHR Services API: authenticates via LoginService.createPeoSession,
/// then extracts client/company data via ClientMasterService.
/// API docs: https://api-docs.prismhr.com
/// </summary>
public sealed class PrismHrPlugin : IIntegrationPlugin
{
    public string Name => "PrismHR";
    public string Description => "Extracts company information from PrismHR payroll system";
    public string Version => "1.0.0";
    public Type ConfigType => typeof(PrismHrConfig);
    public Type CredentialsType => typeof(PrismHrCredentials);

    private PluginContext _context = null!;
    private PrismHrApiClient _apiClient = null!;

    public async Task InitializeAsync(PluginContext context)
    {
        _context = context;

        var baseUrl = context.Configuration.GetValueOrDefault("BaseUrl", "https://api.prismhr.com");
        var peoId = context.Credentials.AdditionalFields.GetValueOrDefault("PeoId", "");

        _apiClient = new PrismHrApiClient(baseUrl, context.Logger);

        context.Logger.LogInformation("Authenticating with PrismHR API at {BaseUrl}", baseUrl);
        await _apiClient.LoginAsync(context.Credentials.Username, context.Credentials.Password, peoId);

        context.Logger.LogInformation("PrismHR authentication successful for request {RequestId}", context.RequestId);
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await _context.OnStarted("Beginning PrismHR company data extraction");

        try
        {
            await _context.OnProgress(1, 3, "Fetching client list from PrismHR");

            var companiesJson = await _apiClient.GetClientListAsync(cancellationToken);

            await _context.OnProgress(2, 3, "Transforming company data to common model");

            var transformer = new PrismHrDataTransformer();
            var commonData = transformer.TransformCompanies(companiesJson);

            await _context.OnProgress(3, 3, "Delivering transformed data");

            using var stream = DataSerializer.SerializeToStream(commonData);
            await _context.OnDataReady("companies", stream);

            await _context.OnCompleted($"Successfully extracted and transformed {commonData.Count} companies");
        }
        catch (Exception ex)
        {
            await _context.OnFailed($"PrismHR extraction failed: {ex.Message}", ex);
            throw;
        }
    }

    public async Task ShutdownAsync()
    {
        if (_apiClient != null)
        {
            await _apiClient.LogoutAsync();
            _apiClient.Dispose();
        }
    }
}

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IntegrationPro.Plugin.SecEdgar.Models;
using IntegrationPro.PluginBase;
using Microsoft.Extensions.Logging;

namespace IntegrationPro.Plugin.SecEdgar;

/// <summary>
/// Plugin that extracts company submissions metadata from SEC EDGAR (https://data.sec.gov).
/// Auth is a User-Agent header per SEC fair-access rules — no account or key required.
/// Returns a JSON array of CommonCompanyDto records, one per requested CIK.
/// </summary>
public sealed class SecEdgarPlugin : IIntegrationPlugin
{
    public string Name => "SecEdgar";
    public string Description => "Extracts company metadata from SEC EDGAR (data.sec.gov). No account required — just a User-Agent identifying the caller.";
    public string Version => "1.0.0";
    public Type ConfigType => typeof(SecEdgarConfig);
    public Type CredentialsType => typeof(SecEdgarCredentials);

    private PluginContext _context = null!;
    private HttpClient _httpClient = null!;

    public Task InitializeAsync(PluginContext context)
    {
        _context = context;
        var baseUrl = context.Configuration.GetValueOrDefault("BaseUrl", "https://data.sec.gov");
        var userAgent = context.Credentials.AdditionalFields.GetValueOrDefault("UserAgent", "");

        if (string.IsNullOrWhiteSpace(userAgent))
            throw new InvalidOperationException(
                "UserAgent credential is required. SEC EDGAR rejects requests without one. Format: 'Name email@example.com'.");

        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        // SEC's User-Agent requirement is free-form text; HttpClient.ParseAdd is strict RFC 7231
        // (Product/Version tokens) and rejects typical "Name email@example.com" values. Skip validation.
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        context.Logger.LogInformation("SEC EDGAR client ready at {BaseUrl}", baseUrl);
        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await _context.OnStarted("Fetching SEC EDGAR submissions");

        var ciksRaw = _context.Configuration.GetValueOrDefault("Ciks", "");
        var ciks = ciksRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.PadLeft(10, '0'))
            .ToList();

        if (ciks.Count == 0)
            throw new InvalidOperationException(
                "Configuration.Ciks is required. Provide a comma-separated list of CIK numbers (e.g., '320193,789019').");

        var companies = new List<CommonCompanyDto>();
        for (int i = 0; i < ciks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cik = ciks[i];
            await _context.OnProgress(i + 1, ciks.Count, $"Fetching CIK {cik}");

            var resp = await _httpClient.GetAsync($"/submissions/CIK{cik}.json", cancellationToken);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"SEC EDGAR returned {(int)resp.StatusCode} for CIK {cik}. Verify the CIK exists and the User-Agent is acceptable to SEC.");

            var json = await resp.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            companies.Add(Transform(doc.RootElement, cik));
        }

        var output = JsonSerializer.Serialize(companies, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(output));
        await _context.OnDataReady("companies", stream);

        await _context.OnCompleted($"Extracted {companies.Count} companies from SEC EDGAR.");
    }

    public Task ShutdownAsync()
    {
        _httpClient?.Dispose();
        return Task.CompletedTask;
    }

    private static CommonCompanyDto Transform(JsonElement root, string cik)
    {
        string? Str(string key) =>
            root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        var name = Str("name") ?? "";
        var ein = Str("ein");
        var sic = Str("sic");
        var sicDesc = Str("sicDescription");
        var state = Str("stateOfIncorporation");
        var entityType = Str("entityType");

        string? firstFormerName = null;
        if (root.TryGetProperty("formerNames", out var fn) && fn.ValueKind == JsonValueKind.Array && fn.GetArrayLength() > 0)
        {
            if (fn[0].TryGetProperty("name", out var fnName))
                firstFormerName = fnName.GetString();
        }

        var tickers = "";
        if (root.TryGetProperty("tickers", out var tk) && tk.ValueKind == JsonValueKind.Array)
            tickers = string.Join(",", tk.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => s.Length > 0));

        AddressDto? address = null;
        if (root.TryGetProperty("addresses", out var addrs) && addrs.ValueKind == JsonValueKind.Object)
            address = TryAddress(addrs, "business") ?? TryAddress(addrs, "mailing");

        var status = entityType?.Equals("operating", StringComparison.OrdinalIgnoreCase) == true
            ? "Active"
            : (entityType ?? "Unknown");

        return new CommonCompanyDto
        {
            SourceSystem = "SecEdgar",
            ExternalId = cik,
            CompanyName = name,
            LegalName = firstFormerName ?? name,
            FederalEin = ein,
            Status = status,
            PrimaryAddress = address,
            ExtendedProperties = new Dictionary<string, string>
            {
                ["sic"] = sic ?? "",
                ["sicDescription"] = sicDesc ?? "",
                ["stateOfIncorporation"] = state ?? "",
                ["entityType"] = entityType ?? "",
                ["tickers"] = tickers,
            }
        };
    }

    private static AddressDto? TryAddress(JsonElement container, string key)
    {
        if (!container.TryGetProperty(key, out var a) || a.ValueKind != JsonValueKind.Object) return null;
        string? Str(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        var line1 = Str("street1");
        var city = Str("city");
        if (line1 is null && city is null) return null;
        return new AddressDto
        {
            Line1 = line1,
            Line2 = Str("street2"),
            City = city,
            State = Str("stateOrCountry"),
            PostalCode = Str("zipCode"),
            Country = null,
        };
    }
}

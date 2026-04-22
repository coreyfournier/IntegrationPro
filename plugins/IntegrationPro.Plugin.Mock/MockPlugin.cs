using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using IntegrationPro.PluginBase;
using Microsoft.Extensions.Logging;

namespace IntegrationPro.Plugin.Mock;

/// <summary>
/// Mock plugin v1.1.0 — generates industry-flavored fake company data with optional
/// deterministic seeding, and supports a set of <see cref="MockFailureMode"/>s that
/// throw different exception types so the host's error classifier can be exercised.
/// </summary>
public sealed class MockPlugin : IIntegrationPlugin
{
    public string Name => "Mock";
    public string Description => "Generates industry-flavored mock company data. Supports deterministic seeding and exercising each error-classifier category.";
    public string Version => "1.1.0";
    public Type ConfigType => typeof(MockConfig);
    public Type CredentialsType => typeof(MockCredentials);

    private PluginContext _context = null!;

    public Task InitializeAsync(PluginContext context)
    {
        _context = context;
        context.Logger.LogInformation("Mock plugin initialized for request {RequestId}", context.RequestId);
        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await _context.OnStarted("Mock extraction started");

        var companyCount = int.Parse(_context.Configuration.GetValueOrDefault("CompanyCount", "5"));
        var delayMs = int.Parse(_context.Configuration.GetValueOrDefault("DelayMs", "100"));
        var industry = ParseEnum(_context.Configuration.GetValueOrDefault("Industry", nameof(MockIndustry.Mixed)), MockIndustry.Mixed);
        var failureMode = ParseEnum(_context.Configuration.GetValueOrDefault("FailureMode", nameof(MockFailureMode.None)), MockFailureMode.None);
        var seed = int.TryParse(_context.Configuration.GetValueOrDefault("Seed", ""), out var s) ? s : (int?)null;

        var rng = seed is null ? new Random() : new Random(seed.Value);
        var companies = new List<MockCompanyData>();

        for (int i = 1; i <= companyCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _context.OnProgress(i, companyCount, $"Generating mock company {i} of {companyCount}");
            companies.Add(Generate(i, rng, industry));

            if (delayMs > 0)
                await Task.Delay(delayMs, cancellationToken);

            if (failureMode != MockFailureMode.None && i == Math.Max(1, companyCount / 2))
                throw BuildFailure(failureMode);
        }

        var json = JsonSerializer.Serialize(companies, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await _context.OnDataReady("companies", stream);

        await _context.OnCompleted($"Mock extraction completed: {companyCount} companies generated ({industry}, seed={seed?.ToString() ?? "random"}).");
    }

    public Task ShutdownAsync()
    {
        _context.Logger.LogInformation("Mock plugin shut down");
        return Task.CompletedTask;
    }

    private static Exception BuildFailure(MockFailureMode mode) => mode switch
    {
        MockFailureMode.Halfway        => new InvalidOperationException("Simulated failure: Halfway — unexpected error at 50% progress."),
        MockFailureMode.AuthFailure    => new UnauthorizedAccessException("Simulated failure: AuthFailure — target system rejected credentials."),
        MockFailureMode.UpstreamHttp   => new HttpRequestException("Simulated failure: UpstreamHttp — target system returned 503.", null, HttpStatusCode.ServiceUnavailable),
        MockFailureMode.UpstreamTimeout => new TaskCanceledException("Simulated failure: UpstreamTimeout — upstream response exceeded client timeout."),
        MockFailureMode.InvalidUri     => new UriFormatException("Simulated failure: InvalidUri — target BaseUrl could not be parsed."),
        _                              => new InvalidOperationException("Unknown failure mode."),
    };

    private static T ParseEnum<T>(string value, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static MockCompanyData Generate(int index, Random rng, MockIndustry industry)
    {
        var profile = ResolveProfile(industry, rng);
        var city = Cities[rng.Next(Cities.Length)];
        var firstName = FirstNames[rng.Next(FirstNames.Length)];
        var lastName = LastNames[rng.Next(LastNames.Length)];

        var companyName = $"{profile.Prefixes[rng.Next(profile.Prefixes.Length)]} {profile.Roots[rng.Next(profile.Roots.Length)]}";
        var slug = companyName.ToLowerInvariant().Replace(" ", "").Replace(".", "");

        return new MockCompanyData
        {
            SourceSystem = "Mock",
            ExternalId = $"MOCK-{index:D5}",
            CompanyName = companyName,
            LegalName = $"{companyName} {profile.Suffixes[rng.Next(profile.Suffixes.Length)]}",
            FederalEin = $"{rng.Next(10, 99):D2}-{rng.Next(1_000_000, 9_999_999):D7}",
            Status = rng.Next(10) == 0 ? "Inactive" : "Active",
            EffectiveDate = DateTime.UtcNow.AddDays(-rng.Next(0, 3650)).ToString("yyyy-MM-dd"),
            SicCode = profile.SicCodes[rng.Next(profile.SicCodes.Length)],
            PrimaryAddress = new MockAddress
            {
                Line1 = $"{rng.Next(100, 9999)} {StreetNames[rng.Next(StreetNames.Length)]} {StreetTypes[rng.Next(StreetTypes.Length)]}",
                City = city.City,
                State = city.State,
                PostalCode = $"{rng.Next(10_000, 99_999):D5}",
                Country = "US",
            },
            PrimaryContact = new MockContact
            {
                FirstName = firstName,
                LastName = lastName,
                Email = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}@{slug}.example",
                Phone = $"({rng.Next(200, 999)}) {rng.Next(200, 999)}-{rng.Next(0, 9999):D4}",
                Title = profile.Titles[rng.Next(profile.Titles.Length)],
            },
        };
    }

    private static IndustryProfile ResolveProfile(MockIndustry industry, Random rng) =>
        industry switch
        {
            MockIndustry.Tech => TechProfile,
            MockIndustry.Retail => RetailProfile,
            MockIndustry.Healthcare => HealthcareProfile,
            MockIndustry.Finance => FinanceProfile,
            _ => AllProfiles[rng.Next(AllProfiles.Length)],
        };

    private sealed record IndustryProfile(string[] Prefixes, string[] Roots, string[] Suffixes, string[] Titles, string[] SicCodes);

    private static readonly IndustryProfile TechProfile = new(
        Prefixes: new[] { "Cloud", "Quantum", "Dataflow", "Cipher", "Vertex", "Lumina" },
        Roots: new[] { "Systems", "Labs", "Analytics", "Compute", "Signals", "Works" },
        Suffixes: new[] { "Inc.", "Corp.", "LLC" },
        Titles: new[] { "VP Engineering", "Head of People", "CTO", "Director of Ops" },
        SicCodes: new[] { "7371", "7372", "7379", "7389" });

    private static readonly IndustryProfile RetailProfile = new(
        Prefixes: new[] { "Highland", "Crescent", "Riverstone", "Northgate", "Ironhill", "Oakwood" },
        Roots: new[] { "Market", "Goods", "Outfitters", "Supply", "Mercantile", "Trading Co" },
        Suffixes: new[] { "LLC", "Inc.", "Co." },
        Titles: new[] { "Store Director", "HR Manager", "Regional VP", "Head of Merchandising" },
        SicCodes: new[] { "5411", "5311", "5712", "5999" });

    private static readonly IndustryProfile HealthcareProfile = new(
        Prefixes: new[] { "Meridian", "Pinewood", "Summit", "Clearbrook", "Cedar Valley", "Harborview" },
        Roots: new[] { "Medical", "Health", "Care Partners", "Clinics", "Wellness", "Physicians" },
        Suffixes: new[] { "PLLC", "PC", "LLC" },
        Titles: new[] { "Chief Medical Officer", "HR Director", "Practice Manager", "VP Operations" },
        SicCodes: new[] { "8011", "8021", "8062", "8093" });

    private static readonly IndustryProfile FinanceProfile = new(
        Prefixes: new[] { "Silverton", "Harbor", "Kingsford", "Ironbridge", "Lakeshore", "Beaumont" },
        Roots: new[] { "Capital", "Trust", "Advisors", "Partners", "Holdings", "Group" },
        Suffixes: new[] { "LP", "LLC", "Inc." },
        Titles: new[] { "Managing Director", "HR Business Partner", "CFO", "Head of Compliance" },
        SicCodes: new[] { "6020", "6199", "6211", "6311" });

    private static readonly IndustryProfile[] AllProfiles = { TechProfile, RetailProfile, HealthcareProfile, FinanceProfile };

    private static readonly (string City, string State)[] Cities =
    {
        ("Seattle", "WA"), ("Austin", "TX"), ("Chicago", "IL"), ("Boston", "MA"),
        ("Denver", "CO"), ("Portland", "OR"), ("Atlanta", "GA"), ("Minneapolis", "MN"),
        ("Raleigh", "NC"), ("Nashville", "TN"), ("Phoenix", "AZ"), ("Pittsburgh", "PA"),
    };
    private static readonly string[] FirstNames = { "Alice", "Marcus", "Priya", "Jordan", "Sofia", "Owen", "Nadia", "Chen", "Maya", "Elliot", "Devin", "Amara" };
    private static readonly string[] LastNames = { "Chen", "Rivera", "Patel", "Thompson", "Nakamura", "Walsh", "Okafor", "Delgado", "Park", "Hansen", "Nguyen", "Brooks" };
    private static readonly string[] StreetNames = { "Market", "Union", "Cedar", "Lakeshore", "Willow", "Park", "Hillcrest", "Commerce", "Summit", "Pine" };
    private static readonly string[] StreetTypes = { "Street", "Avenue", "Boulevard", "Way", "Drive", "Lane" };
}

internal sealed class MockCompanyData
{
    public string SourceSystem { get; init; } = string.Empty;
    public string ExternalId { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string? LegalName { get; init; }
    public string? FederalEin { get; init; }
    public string? Status { get; init; }
    public string? EffectiveDate { get; init; }
    public string? SicCode { get; init; }
    public MockAddress? PrimaryAddress { get; init; }
    public MockContact? PrimaryContact { get; init; }
}

internal sealed class MockAddress
{
    public string? Line1 { get; init; }
    public string? Line2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
}

internal sealed class MockContact
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Title { get; init; }
}

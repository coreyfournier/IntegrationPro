using System.Text;
using System.Text.Json;
using IntegrationPro.PluginBase;
using Microsoft.Extensions.Logging;

namespace IntegrationPro.Plugin.Mock;

/// <summary>
/// Stubbed-out plugin for testing that generates mock company data.
/// Exercises all callback methods (OnStarted, OnProgress, OnDataReady, OnCompleted, OnFailed).
/// </summary>
public sealed class MockPlugin : IIntegrationPlugin
{
    public string Name => "Mock";
    public string Description => "Generates mock company data for testing the ETL pipeline";
    public string Version => "1.0.0";
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

        var simulateFailure = _context.Configuration.GetValueOrDefault("SimulateFailure", "false");
        var companyCount = int.Parse(_context.Configuration.GetValueOrDefault("CompanyCount", "5"));
        var delayMs = int.Parse(_context.Configuration.GetValueOrDefault("DelayMs", "100"));

        var companies = new List<MockCompanyData>();

        for (int i = 1; i <= companyCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _context.OnProgress(i, companyCount, $"Generating mock company {i} of {companyCount}");

            companies.Add(GenerateMockCompany(i));

            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }

            // Simulate a failure mid-extraction if configured
            if (simulateFailure == "true" && i == companyCount / 2)
            {
                var ex = new InvalidOperationException("Simulated extraction failure for testing");
                await _context.OnFailed("Simulated failure during extraction", ex);
                throw ex;
            }
        }

        var json = JsonSerializer.Serialize(companies, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await _context.OnDataReady("companies", stream);

        await _context.OnCompleted($"Mock extraction completed: {companyCount} companies generated");
    }

    public Task ShutdownAsync()
    {
        _context.Logger.LogInformation("Mock plugin shut down");
        return Task.CompletedTask;
    }

    private static MockCompanyData GenerateMockCompany(int index)
    {
        return new MockCompanyData
        {
            SourceSystem = "Mock",
            ExternalId = $"MOCK-{index:D5}",
            CompanyName = $"Mock Company {index}",
            LegalName = $"Mock Company {index} LLC",
            FederalEin = $"{10 + index:D2}-{1000000 + index:D7}",
            Status = index % 4 == 0 ? "Inactive" : "Active",
            EffectiveDate = DateTime.UtcNow.AddDays(-index * 30).ToString("yyyy-MM-dd"),
            PrimaryAddress = new MockAddress
            {
                Line1 = $"{100 + index} Main Street",
                City = "Springfield",
                State = "IL",
                PostalCode = $"{60000 + index}",
                Country = "US"
            },
            PrimaryContact = new MockContact
            {
                FirstName = $"John",
                LastName = $"Doe{index}",
                Email = $"john.doe{index}@mockcompany{index}.com",
                Phone = $"555-{1000 + index:D4}",
                Title = "HR Manager"
            }
        };
    }
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

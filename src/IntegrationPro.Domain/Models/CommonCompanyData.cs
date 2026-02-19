namespace IntegrationPro.Domain.Models;

/// <summary>
/// Simplified common data model for company information extracted from any source.
/// Plugins transform source-specific data into this common format.
/// </summary>
public sealed class CommonCompanyData
{
    public string SourceSystem { get; init; } = string.Empty;
    public string ExternalId { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string? LegalName { get; init; }
    public string? FederalEin { get; init; }
    public string? Status { get; init; }

    public CommonAddress? PrimaryAddress { get; init; }
    public CommonContact? PrimaryContact { get; init; }

    public DateTime? EffectiveDate { get; init; }
    public IReadOnlyDictionary<string, string> ExtendedProperties { get; init; }
        = new Dictionary<string, string>();
}

public sealed class CommonAddress
{
    public string? Line1 { get; init; }
    public string? Line2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
}

public sealed class CommonContact
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Title { get; init; }
}

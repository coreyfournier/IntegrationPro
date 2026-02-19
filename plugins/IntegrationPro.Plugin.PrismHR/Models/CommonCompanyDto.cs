namespace IntegrationPro.Plugin.PrismHR.Models;

/// <summary>
/// Simplified common data model for company information, used as the transformation
/// output before serializing to a data stream for the host application.
/// </summary>
internal sealed class CommonCompanyDto
{
    public string SourceSystem { get; init; } = string.Empty;
    public string ExternalId { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string? LegalName { get; init; }
    public string? FederalEin { get; init; }
    public string? Status { get; init; }
    public DateTime? EffectiveDate { get; init; }
    public CommonAddressDto? PrimaryAddress { get; init; }
    public CommonContactDto? PrimaryContact { get; init; }
    public Dictionary<string, string> ExtendedProperties { get; init; } = new();
}

internal sealed class CommonAddressDto
{
    public string? Line1 { get; init; }
    public string? Line2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
}

internal sealed class CommonContactDto
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Title { get; init; }
}

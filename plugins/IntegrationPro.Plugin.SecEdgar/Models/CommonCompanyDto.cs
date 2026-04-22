namespace IntegrationPro.Plugin.SecEdgar.Models;

internal sealed class CommonCompanyDto
{
    public string SourceSystem { get; init; } = "";
    public string ExternalId { get; init; } = "";
    public string CompanyName { get; init; } = "";
    public string? LegalName { get; init; }
    public string? FederalEin { get; init; }
    public string? Status { get; init; }
    public AddressDto? PrimaryAddress { get; init; }
    public Dictionary<string, string> ExtendedProperties { get; init; } = new();
}

internal sealed class AddressDto
{
    public string? Line1 { get; init; }
    public string? Line2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
}

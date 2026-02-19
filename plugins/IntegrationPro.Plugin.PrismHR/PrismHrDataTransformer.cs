using System.Text.Json;
using IntegrationPro.Plugin.PrismHR.Models;

namespace IntegrationPro.Plugin.PrismHR;

/// <summary>
/// Transforms PrismHR client/company JSON responses into the simplified common data model.
/// </summary>
internal sealed class PrismHrDataTransformer
{
    public List<CommonCompanyDto> TransformCompanies(string jsonResponse)
    {
        var result = new List<CommonCompanyDto>();

        using var doc = JsonDocument.Parse(jsonResponse);
        var root = doc.RootElement;

        // PrismHR returns clients in a "clients" array or as a root array
        var clientsElement = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("clients", out var clients) ? clients : root;

        foreach (var client in clientsElement.EnumerateArray())
        {
            result.Add(MapClient(client));
        }

        return result;
    }

    private static CommonCompanyDto MapClient(JsonElement client)
    {
        return new CommonCompanyDto
        {
            SourceSystem = "PrismHR",
            ExternalId = GetStringOrDefault(client, "clientId"),
            CompanyName = GetStringOrDefault(client, "clientName"),
            LegalName = GetStringOrDefault(client, "legalName"),
            FederalEin = GetStringOrDefault(client, "federalEin"),
            Status = GetStringOrDefault(client, "status"),
            EffectiveDate = GetDateOrDefault(client, "effectiveDate"),
            PrimaryAddress = MapAddress(client),
            PrimaryContact = MapContact(client),
            ExtendedProperties = ExtractExtendedProperties(client)
        };
    }

    private static CommonAddressDto? MapAddress(JsonElement client)
    {
        if (!client.TryGetProperty("address", out var addr))
            return null;

        return new CommonAddressDto
        {
            Line1 = GetStringOrDefault(addr, "address1"),
            Line2 = GetStringOrDefault(addr, "address2"),
            City = GetStringOrDefault(addr, "city"),
            State = GetStringOrDefault(addr, "state"),
            PostalCode = GetStringOrDefault(addr, "zip"),
            Country = GetStringOrDefault(addr, "country")
        };
    }

    private static CommonContactDto? MapContact(JsonElement client)
    {
        if (!client.TryGetProperty("contact", out var contact))
            return null;

        return new CommonContactDto
        {
            FirstName = GetStringOrDefault(contact, "firstName"),
            LastName = GetStringOrDefault(contact, "lastName"),
            Email = GetStringOrDefault(contact, "email"),
            Phone = GetStringOrDefault(contact, "phone"),
            Title = GetStringOrDefault(contact, "title")
        };
    }

    private static Dictionary<string, string> ExtractExtendedProperties(JsonElement client)
    {
        var props = new Dictionary<string, string>();

        TryAdd(props, client, "sic");
        TryAdd(props, client, "naics");
        TryAdd(props, client, "stateOfIncorporation");
        TryAdd(props, client, "payFrequency");

        return props;
    }

    private static string GetStringOrDefault(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? string.Empty
            : string.Empty;
    }

    private static DateTime? GetDateOrDefault(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return DateTime.TryParse(prop.GetString(), out var date) ? date : null;
        }
        return null;
    }

    private static void TryAdd(Dictionary<string, string> dict, JsonElement element, string property)
    {
        var value = GetStringOrDefault(element, property);
        if (!string.IsNullOrEmpty(value))
        {
            dict[property] = value;
        }
    }
}

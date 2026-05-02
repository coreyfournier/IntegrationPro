using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace IntegrationPro.Plugin.PrismHR;

public sealed class PrismHrConfig
{
    [Description("Base URL of the PrismHR API endpoint.")]
    public string BaseUrl { get; init; } = "https://api.prismhr.com";
}

public sealed class PrismHrCredentials
{
    [Required, Description("PrismHR username1.")]
    public string Username { get; init; } = "";

    [Required, Description("PrismHR password.")]
    public string Password { get; init; } = "";

    [Required, Description("PrismHR PEO identifier used in createPeoSession.")]
    public string PeoId { get; init; } = "";
}

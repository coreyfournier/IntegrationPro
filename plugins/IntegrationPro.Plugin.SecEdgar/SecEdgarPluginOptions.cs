using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace IntegrationPro.Plugin.SecEdgar;

public sealed class SecEdgarConfig
{
    [Description("SEC EDGAR API base URL.")]
    public string BaseUrl { get; init; } = "https://data.sec.gov";

    [Required, Description("Comma-separated CIK numbers to fetch (e.g., '320193,789019'). Leading zeros are padded automatically.")]
    public string Ciks { get; init; } = "";
}

public sealed class SecEdgarCredentials
{
    [Required, Description("User-Agent sent with every request. SEC requires identifying the caller (e.g., 'Sample Company admin@example.com').")]
    public string UserAgent { get; init; } = "";
}

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace IntegrationPro.Plugin.Mock;

public sealed class MockConfig
{
    [Description("Number of mock companies to generate.")]
    [Range(1, 1000)]
    public int CompanyCount { get; init; } = 5;

    [Description("Delay between generated records, in milliseconds.")]
    [Range(0, 60_000)]
    public int DelayMs { get; init; } = 100;

    [Description("If true, the plugin throws halfway through to exercise the failure path.")]
    public bool SimulateFailure { get; init; } = false;
}

public sealed class MockCredentials
{
    [Required, Description("Unused by the mock; present so the playground renders a credentials form.")]
    public string Username { get; init; } = "";

    [Required, Description("Unused by the mock.")]
    public string Password { get; init; } = "";
}

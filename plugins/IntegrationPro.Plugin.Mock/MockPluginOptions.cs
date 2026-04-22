using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace IntegrationPro.Plugin.Mock;

public sealed class MockConfig
{
    [Description("Number of mock companies to generate.")]
    [DefaultValue(5)]
    [Range(1, 1000)]
    public int CompanyCount { get; init; } = 5;

    [Description("Delay between generated records, in milliseconds.")]
    [DefaultValue(100)]
    [Range(0, 60_000)]
    public int DelayMs { get; init; } = 100;

    [Description("Optional RNG seed. Set for deterministic output; leave empty for time-based randomness.")]
    public int? Seed { get; init; }

    [Description("Industry profile to bias company names, titles, and SIC codes.")]
    [DefaultValue(MockIndustry.Mixed)]
    public MockIndustry Industry { get; init; } = MockIndustry.Mixed;

    [Description("Intentional failure mode. 'None' runs to completion; others throw different exception types to exercise the error classifier.")]
    [DefaultValue(MockFailureMode.None)]
    public MockFailureMode FailureMode { get; init; } = MockFailureMode.None;
}

public sealed class MockCredentials
{
    [Required, Description("Unused by the mock; present so the playground renders a credentials form.")]
    public string Username { get; init; } = "";

    [Required, Description("Unused by the mock.")]
    public string Password { get; init; } = "";
}

public enum MockIndustry
{
    Mixed,
    Tech,
    Retail,
    Healthcare,
    Finance,
}

public enum MockFailureMode
{
    None,
    Halfway,
    AuthFailure,
    UpstreamHttp,
    UpstreamTimeout,
    InvalidUri,
}

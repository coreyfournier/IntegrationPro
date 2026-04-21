using FluentAssertions;
using IntegrationPro.Application.Catalog;
using IntegrationPro.Application.PluginLoading;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntegrationPro.Application.Tests;

public sealed class DiskReflectionPluginCatalogTests : IAsyncLifetime
{
    private string _pluginsDir = "";

    public async Task InitializeAsync()
    {
        _pluginsDir = Path.Combine(Path.GetTempPath(), "ipro-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_pluginsDir);

        var mockCsproj = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "plugins", "IntegrationPro.Plugin.Mock", "IntegrationPro.Plugin.Mock.csproj"));
        var target = Path.Combine(_pluginsDir, "IntegrationPro.Plugin.Mock", "1.0.0");
        var psi = new System.Diagnostics.ProcessStartInfo("dotnet",
            $"publish \"{mockCsproj}\" -c Release -o \"{target}\"")
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false
        };
        using var proc = System.Diagnostics.Process.Start(psi)!;
        await proc.WaitForExitAsync();
        proc.ExitCode.Should().Be(0,
            "dotnet publish for the Mock plugin must succeed. stderr:\n" +
            await proc.StandardError.ReadToEndAsync());
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_pluginsDir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ListPlugins_returns_Mock_with_friendly_name()
    {
        var loader = new PluginLoader(_pluginsDir, NullLogger<PluginLoader>.Instance);
        var catalog = new DiskReflectionPluginCatalog(loader, NullLogger<DiskReflectionPluginCatalog>.Instance);
        await catalog.InitializeAsync();

        var summaries = catalog.ListPlugins();
        summaries.Should().ContainSingle(s => s.Name == "Mock")
            .Which.LatestVersion.Should().Be("1.0.0");
    }

    [Fact]
    public async Task ListVersions_returns_versions_for_known_plugin()
    {
        var loader = new PluginLoader(_pluginsDir, NullLogger<PluginLoader>.Instance);
        var catalog = new DiskReflectionPluginCatalog(loader, NullLogger<DiskReflectionPluginCatalog>.Instance);
        await catalog.InitializeAsync();

        catalog.ListVersions("Mock").Should().Equal("1.0.0");
    }

    [Fact]
    public async Task GetSchemaAsync_produces_schemas_with_Description_from_DataAnnotations()
    {
        var loader = new PluginLoader(_pluginsDir, NullLogger<PluginLoader>.Instance);
        var catalog = new DiskReflectionPluginCatalog(loader, NullLogger<DiskReflectionPluginCatalog>.Instance);
        await catalog.InitializeAsync();

        var schema = await catalog.GetSchemaAsync("Mock", "1.0.0");

        schema.Name.Should().Be("Mock");
        schema.Version.Should().Be("1.0.0");
        schema.Config.Properties.Should().ContainKey("companyCount");
        schema.Credentials.Properties.Should().ContainKey("username");
    }

    [Fact]
    public async Task ResolveAsync_with_null_version_returns_latest_instance()
    {
        var loader = new PluginLoader(_pluginsDir, NullLogger<PluginLoader>.Instance);
        var catalog = new DiskReflectionPluginCatalog(loader, NullLogger<DiskReflectionPluginCatalog>.Instance);
        await catalog.InitializeAsync();

        var plugin = await catalog.ResolveAsync("Mock", version: null);
        plugin.Name.Should().Be("Mock");
        plugin.Version.Should().Be("1.0.0");
    }
}

using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace IntegrationPro.Api.Tests;

public sealed class ApiTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string PluginsDir { get; private set; } = "";

    public async Task InitializeAsync()
    {
        PluginsDir = Path.Combine(Path.GetTempPath(), "ipro-api-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(PluginsDir);

        var mockCsproj = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "plugins", "IntegrationPro.Plugin.Mock", "IntegrationPro.Plugin.Mock.csproj"));
        var target = Path.Combine(PluginsDir, "IntegrationPro.Plugin.Mock", "1.0.0");

        var psi = new ProcessStartInfo("dotnet",
            $"publish \"{mockCsproj}\" -c Release -o \"{target}\"")
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        using var proc = Process.Start(psi)!;
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException("publish failed: " + await proc.StandardError.ReadToEndAsync());

        var dll = Path.Combine(target, "IntegrationPro.Plugin.Mock.dll");
        if (!File.Exists(dll))
            throw new InvalidOperationException(
                $"Publish did not produce expected DLL at '{dll}'. Stdout: {await proc.StandardOutput.ReadToEndAsync()}");
    }

    public new Task DisposeAsync()
    {
        try { Directory.Delete(PluginsDir, recursive: true); } catch { }
        return base.DisposeAsync().AsTask();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Plugins:Directory", PluginsDir);
        builder.UseSetting("DataOutput:Directory", Path.Combine(PluginsDir, "_out"));
    }
}

# Synchronous Integration API Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a synchronous HTTP API + playground UI on top of IntegrationPro, with a plugin catalog whose schemas are discoverable via JSON Schema / OpenAPI, while keeping the existing Service Bus path working.

**Architecture:** New `IntegrationPro.Api` ASP.NET Core project sibling to `Worker`. Plugin contract grows POCO-based config/credentials types. New `IPluginCatalog` abstraction (today: disk + reflection + NJsonSchema; future swap-in: NuGet-feed lazy pull). Response body is buffered via `FileBufferingWriteStream` so mid-run failures still return proper 4xx/5xx. Playground UI is a Vite + React + RJSF static SPA served from `Api/wwwroot/ui/`.

**Tech Stack:** .NET 8, ASP.NET Core, Swashbuckle, NJsonSchema, xUnit + FluentAssertions, Vite, React, TypeScript, `@rjsf/core`, `@rjsf/validator-ajv8`.

**Design reference:** `docs/plans/2026-04-21-sync-integration-api-design.md`.

---

## Phase 1 — Plugin contract + POCO migration

### Task 1.1: Add Version / ConfigType / CredentialsType to `IIntegrationPlugin`

**Files:**
- Modify: `src/IntegrationPro.PluginBase/IIntegrationPlugin.cs`

**Step 1: Edit the interface**

```csharp
using Microsoft.Extensions.Logging;

namespace IntegrationPro.PluginBase;

public interface IIntegrationPlugin
{
    string Name { get; }
    string Description { get; }

    /// <summary>Semantic version of this plugin build, e.g. "1.0.0".</summary>
    string Version { get; }

    /// <summary>POCO type describing plugin configuration for schema generation.</summary>
    Type ConfigType { get; }

    /// <summary>POCO type describing required credentials for schema generation.</summary>
    Type CredentialsType { get; }

    Task InitializeAsync(PluginContext context);
    Task ExecuteAsync(CancellationToken cancellationToken);
    Task ShutdownAsync();
}
```

**Step 2: Build PluginBase only — expect build failures in plugins**

Run: `dotnet build src/IntegrationPro.PluginBase/IntegrationPro.PluginBase.csproj`
Expected: PASS.

`dotnet build IntegrationPro.sln` at this point fails on Mock/PrismHR — that's expected; we fix them next.

**Step 3: Do not commit yet.** Contract-only commits leave the solution in a broken state. We commit after 1.5 once the interface change + both plugin migrations are done.

---

### Task 1.2: Add Mock plugin config + credentials POCOs

**Files:**
- Create: `plugins/IntegrationPro.Plugin.Mock/MockPluginOptions.cs`

**Step 1: Create the POCOs**

```csharp
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
```

**Step 2: Build the plugin project**

Run: `dotnet build plugins/IntegrationPro.Plugin.Mock/IntegrationPro.Plugin.Mock.csproj`
Expected: still fails — `MockPlugin` doesn't implement new interface members yet. Proceed to 1.3.

---

### Task 1.3: Migrate `MockPlugin` to the new contract

**Files:**
- Modify: `plugins/IntegrationPro.Plugin.Mock/MockPlugin.cs`

**Step 1: Add the new interface members**

At the top of the class, next to `Name`/`Description`:

```csharp
public string Version => "1.0.0";
public Type ConfigType => typeof(MockConfig);
public Type CredentialsType => typeof(MockCredentials);
```

**Step 2: Build the plugin**

Run: `dotnet build plugins/IntegrationPro.Plugin.Mock/IntegrationPro.Plugin.Mock.csproj`
Expected: PASS.

**Step 3: Do not change the runtime logic** — `ExecuteAsync` still consumes `Configuration` as a dictionary because the orchestrator flattens typed input into that shape. No behavior change.

---

### Task 1.4: Add PrismHR plugin config + credentials POCOs

**Files:**
- Create: `plugins/IntegrationPro.Plugin.PrismHR/PrismHrPluginOptions.cs`

**Step 1: Create the POCOs**

```csharp
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
    [Required, Description("PrismHR username.")]
    public string Username { get; init; } = "";

    [Required, Description("PrismHR password.")]
    public string Password { get; init; } = "";

    [Required, Description("PrismHR PEO identifier used in createPeoSession.")]
    public string PeoId { get; init; } = "";
}
```

**Step 2: Build the plugin**

Run: `dotnet build plugins/IntegrationPro.Plugin.PrismHR/IntegrationPro.Plugin.PrismHR.csproj`
Expected: still fails — `PrismHrPlugin` missing new members. Proceed.

---

### Task 1.5: Migrate `PrismHrPlugin` to the new contract + commit Phase 1

**Files:**
- Modify: `plugins/IntegrationPro.Plugin.PrismHR/PrismHrPlugin.cs`

**Step 1: Add the new members**

Next to `Name`/`Description`:

```csharp
public string Version => "1.0.0";
public Type ConfigType => typeof(PrismHrConfig);
public Type CredentialsType => typeof(PrismHrCredentials);
```

Also in `InitializeAsync`: note that `PeoId` moves from `AdditionalFields` to the `PrismHrCredentials` POCO. The runtime flattening (Phase 4) still puts it into `AdditionalFields["PeoId"]`, so the existing line `context.Credentials.AdditionalFields.GetValueOrDefault("PeoId", "")` keeps working. **Do not change the PrismHR runtime logic in this task.**

**Step 2: Build the whole solution**

Run: `dotnet build IntegrationPro.sln`
Expected: PASS.

**Step 3: Commit**

```bash
git add src/IntegrationPro.PluginBase/IIntegrationPlugin.cs \
        plugins/IntegrationPro.Plugin.Mock/MockPluginOptions.cs \
        plugins/IntegrationPro.Plugin.Mock/MockPlugin.cs \
        plugins/IntegrationPro.Plugin.PrismHR/PrismHrPluginOptions.cs \
        plugins/IntegrationPro.Plugin.PrismHR/PrismHrPlugin.cs
git commit -m "Add typed config/credentials to plugin contract"
```

---

## Phase 2 — Plugin directory layout + loader

The current layout is `/app/plugins/{AssemblyDirName}/{AssemblyDirName}.dll`. We move to `/app/plugins/{AssemblyDirName}/{Version}/{AssemblyDirName}.dll`, keyed by the assembly directory name (not the plugin's friendly `Name`). The catalog will later map `PluginName` → `AssemblyDirName` after loading each plugin.

### Task 2.1: Create `IPluginCatalog` contract types (Application layer, stubs only)

**Files:**
- Create: `src/IntegrationPro.Application/Catalog/PluginSummary.cs`
- Create: `src/IntegrationPro.Application/Catalog/PluginSchema.cs`
- Create: `src/IntegrationPro.Application/Catalog/IPluginCatalog.cs`

**Step 1: Add types**

`PluginSummary.cs`:
```csharp
namespace IntegrationPro.Application.Catalog;

public sealed record PluginSummary(string Name, string LatestVersion, string Description);
```

`PluginSchema.cs`:
```csharp
using NJsonSchema;

namespace IntegrationPro.Application.Catalog;

public sealed record PluginSchema(
    string Name,
    string Version,
    string Description,
    JsonSchema Config,
    JsonSchema Credentials);
```

`IPluginCatalog.cs`:
```csharp
using IntegrationPro.PluginBase;

namespace IntegrationPro.Application.Catalog;

public interface IPluginCatalog
{
    IReadOnlyList<PluginSummary> ListPlugins();
    IReadOnlyList<string> ListVersions(string pluginName);
    PluginSchema GetSchema(string pluginName, string version);

    /// <summary>Resolve a plugin instance. Null version => latest.</summary>
    IIntegrationPlugin Resolve(string pluginName, string? version);
}
```

**Step 2: Add NJsonSchema reference to Application**

Modify: `src/IntegrationPro.Application/IntegrationPro.Application.csproj`

Add inside the existing `<ItemGroup>` with PackageReferences (create one if needed):
```xml
<PackageReference Include="NJsonSchema" Version="11.0.2" />
```

**Step 3: Build**

Run: `dotnet build src/IntegrationPro.Application/IntegrationPro.Application.csproj`
Expected: PASS.

**Step 4: Commit**

```bash
git add src/IntegrationPro.Application/Catalog/ src/IntegrationPro.Application/IntegrationPro.Application.csproj
git commit -m "Add IPluginCatalog abstraction"
```

---

### Task 2.2: Update `PluginLoader` to support versioned directory layout

**Files:**
- Modify: `src/IntegrationPro.Application/PluginLoading/PluginLoader.cs`

**Step 1: Change lookup convention**

Replace the body of `LoadPlugin` so the method resolves `{_pluginsDirectory}/{pluginName}/{version}/{pluginName}.dll`, defaulting to the highest-semver subdirectory when `version` is null.

```csharp
public IIntegrationPlugin LoadPlugin(string pluginName, string? version = null)
{
    var pluginDir = Path.Combine(_pluginsDirectory, pluginName);
    if (!Directory.Exists(pluginDir))
    {
        throw new DirectoryNotFoundException(
            $"Plugin directory not found: '{pluginDir}'.");
    }

    var resolvedVersion = version ?? ResolveLatestVersion(pluginDir);
    var pluginDll = Path.Combine(pluginDir, resolvedVersion, $"{pluginName}.dll");

    if (!File.Exists(pluginDll))
    {
        throw new FileNotFoundException(
            $"Plugin assembly not found at '{pluginDll}'.");
    }

    _logger.LogInformation("Loading plugin from {PluginPath}", pluginDll);
    var loadContext = new PluginLoadContext(pluginDll);
    var assembly = loadContext.LoadFromAssemblyName(
        new AssemblyName(Path.GetFileNameWithoutExtension(pluginDll)));
    return CreatePlugin(assembly);
}

public IReadOnlyList<string> ListVersions(string pluginName)
{
    var pluginDir = Path.Combine(_pluginsDirectory, pluginName);
    if (!Directory.Exists(pluginDir)) return Array.Empty<string>();
    return Directory.EnumerateDirectories(pluginDir)
        .Select(Path.GetFileName)
        .Where(n => !string.IsNullOrEmpty(n))
        .OrderByDescending(ParseVersion)
        .ToList()!;
}

public IReadOnlyList<string> ListPluginDirectories()
{
    if (!Directory.Exists(_pluginsDirectory)) return Array.Empty<string>();
    return Directory.EnumerateDirectories(_pluginsDirectory)
        .Select(Path.GetFileName)
        .Where(n => !string.IsNullOrEmpty(n))
        .ToList()!;
}

private static Version ParseVersion(string s) =>
    Version.TryParse(s, out var v) ? v : new Version(0, 0, 0);

private string ResolveLatestVersion(string pluginDir)
{
    var versions = Directory.EnumerateDirectories(pluginDir)
        .Select(Path.GetFileName)
        .Where(n => !string.IsNullOrEmpty(n))
        .OrderByDescending(ParseVersion)
        .ToList();
    if (versions.Count == 0)
        throw new InvalidOperationException($"No versions present under '{pluginDir}'.");
    return versions[0]!;
}
```

**Step 2: Build**

Run: `dotnet build src/IntegrationPro.Application/IntegrationPro.Application.csproj`
Expected: PASS.

**Step 3: Do not commit yet** — consumers (Worker orchestrator, TestHarness) still call the old single-arg signature. We fix those next.

---

### Task 2.3: Update `IntegrationOrchestrator` to pass null version (latest) through

**Files:**
- Modify: `src/IntegrationPro.Application/Services/IntegrationOrchestrator.cs`

**Step 1: Change the single call site**

Replace:
```csharp
plugin = _pluginLoader.LoadPlugin(message.PluginName);
```
with:
```csharp
plugin = _pluginLoader.LoadPlugin(message.PluginName, version: null);
```

This makes the ServiceBus path resolve to latest by default. No message-schema change.

**Step 2: Build**

Run: `dotnet build src/IntegrationPro.Application/IntegrationPro.Application.csproj`
Expected: PASS.

---

### Task 2.4: Update TestHarness + Dockerfile for the new layout, then commit

**Files:**
- Modify: `tests/IntegrationPro.TestHarness/Program.cs`
- Modify: `Dockerfile`
- Modify: `CLAUDE.md` (publish commands)

**Step 1: TestHarness no longer needs code change** (it calls `LoadPlugin("IntegrationPro.Plugin.Mock")` which now resolves latest). But the default `plugins-output` path must follow the new layout. Update the publish command we document.

**Step 2: Update `Dockerfile`**

Replace the two plugin publish lines:

```dockerfile
RUN dotnet publish plugins/IntegrationPro.Plugin.PrismHR/IntegrationPro.Plugin.PrismHR.csproj -c Release -o /app/plugins/IntegrationPro.Plugin.PrismHR/1.0.0
RUN dotnet publish plugins/IntegrationPro.Plugin.Mock/IntegrationPro.Plugin.Mock.csproj         -c Release -o /app/plugins/IntegrationPro.Plugin.Mock/1.0.0
```

**Step 3: Update `CLAUDE.md` publish examples**

Replace the two plugin-publish bash lines with:

```bash
dotnet publish plugins/IntegrationPro.Plugin.PrismHR/IntegrationPro.Plugin.PrismHR.csproj -c Release -o ./plugins-output/IntegrationPro.Plugin.PrismHR/1.0.0
dotnet publish plugins/IntegrationPro.Plugin.Mock/IntegrationPro.Plugin.Mock.csproj       -c Release -o ./plugins-output/IntegrationPro.Plugin.Mock/1.0.0
```

**Step 4: Verify end-to-end**

Run (from repo root):
```bash
rm -rf plugins-output
dotnet publish plugins/IntegrationPro.Plugin.Mock/IntegrationPro.Plugin.Mock.csproj -c Release -o ./plugins-output/IntegrationPro.Plugin.Mock/1.0.0
dotnet run --project tests/IntegrationPro.TestHarness/IntegrationPro.TestHarness.csproj
```
Expected: logs show `Loaded plugin: Mock - ...` and `=== Test Complete ===` with one data batch received.

**Step 5: Commit**

```bash
git add src/IntegrationPro.Application/PluginLoading/PluginLoader.cs \
        src/IntegrationPro.Application/Services/IntegrationOrchestrator.cs \
        Dockerfile CLAUDE.md
git commit -m "Switch plugin layout to {Name}/{Version}/ and resolve latest by default"
```

---

## Phase 3 — Disk + reflection plugin catalog

### Task 3.1: Write failing unit tests for `DiskReflectionPluginCatalog`

**Files:**
- Create: `tests/IntegrationPro.Application.Tests/IntegrationPro.Application.Tests.csproj`
- Create: `tests/IntegrationPro.Application.Tests/DiskReflectionPluginCatalogTests.cs`

**Step 1: Add the test project**

`IntegrationPro.Application.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\IntegrationPro.Application\IntegrationPro.Application.csproj" />
    <ProjectReference Include="..\..\src\IntegrationPro.PluginBase\IntegrationPro.PluginBase.csproj" />
    <ProjectReference Include="..\..\plugins\IntegrationPro.Plugin.Mock\IntegrationPro.Plugin.Mock.csproj">
      <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
    </ProjectReference>
  </ItemGroup>
</Project>
```

**Step 2: Add the test file**

```csharp
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

        // Publish the Mock plugin into /IntegrationPro.Plugin.Mock/1.0.0/
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
    public void ListPlugins_returns_Mock_with_friendly_name()
    {
        var loader = new PluginLoader(_pluginsDir, NullLogger<PluginLoader>.Instance);
        var catalog = new DiskReflectionPluginCatalog(loader, NullLogger<DiskReflectionPluginCatalog>.Instance);

        catalog.Initialize();

        var summaries = catalog.ListPlugins();
        summaries.Should().ContainSingle(s => s.Name == "Mock")
            .Which.LatestVersion.Should().Be("1.0.0");
    }

    [Fact]
    public void ListVersions_returns_versions_for_known_plugin()
    {
        var loader = new PluginLoader(_pluginsDir, NullLogger<PluginLoader>.Instance);
        var catalog = new DiskReflectionPluginCatalog(loader, NullLogger<DiskReflectionPluginCatalog>.Instance);
        catalog.Initialize();

        catalog.ListVersions("Mock").Should().Equal("1.0.0");
    }

    [Fact]
    public void GetSchema_produces_schemas_with_Description_from_DataAnnotations()
    {
        var loader = new PluginLoader(_pluginsDir, NullLogger<PluginLoader>.Instance);
        var catalog = new DiskReflectionPluginCatalog(loader, NullLogger<DiskReflectionPluginCatalog>.Instance);
        catalog.Initialize();

        var schema = catalog.GetSchema("Mock", "1.0.0");

        schema.Name.Should().Be("Mock");
        schema.Version.Should().Be("1.0.0");
        schema.Config.Properties.Should().ContainKey("companyCount");
        schema.Credentials.Properties.Should().ContainKey("username");
    }

    [Fact]
    public void Resolve_with_null_version_returns_latest_instance()
    {
        var loader = new PluginLoader(_pluginsDir, NullLogger<PluginLoader>.Instance);
        var catalog = new DiskReflectionPluginCatalog(loader, NullLogger<DiskReflectionPluginCatalog>.Instance);
        catalog.Initialize();

        var plugin = catalog.Resolve("Mock", version: null);
        plugin.Name.Should().Be("Mock");
        plugin.Version.Should().Be("1.0.0");
    }
}
```

**Step 3: Add the test project to the solution**

Run (from repo root):
```bash
dotnet sln add tests/IntegrationPro.Application.Tests/IntegrationPro.Application.Tests.csproj
```

**Step 4: Run tests to see them fail**

Run: `dotnet test tests/IntegrationPro.Application.Tests/IntegrationPro.Application.Tests.csproj`
Expected: FAIL — `DiskReflectionPluginCatalog` does not exist yet.

---

### Task 3.2: Implement `DiskReflectionPluginCatalog`

**Files:**
- Create: `src/IntegrationPro.Application/Catalog/DiskReflectionPluginCatalog.cs`

**Step 1: Create the implementation**

```csharp
using System.Collections.Concurrent;
using IntegrationPro.Application.PluginLoading;
using IntegrationPro.PluginBase;
using Microsoft.Extensions.Logging;
using NJsonSchema;
using NJsonSchema.Generation;

namespace IntegrationPro.Application.Catalog;

public sealed class DiskReflectionPluginCatalog : IPluginCatalog
{
    private readonly PluginLoader _loader;
    private readonly ILogger<DiskReflectionPluginCatalog> _logger;

    // friendly Name -> entries by version
    private readonly ConcurrentDictionary<string, SortedDictionary<Version, Entry>> _index = new();

    public DiskReflectionPluginCatalog(PluginLoader loader, ILogger<DiskReflectionPluginCatalog> logger)
    {
        _loader = loader;
        _logger = logger;
    }

    public void Initialize()
    {
        foreach (var pluginDir in _loader.ListPluginDirectories())
        {
            foreach (var version in _loader.ListVersions(pluginDir))
            {
                try
                {
                    var plugin = _loader.LoadPlugin(pluginDir, version);
                    var entry = new Entry(
                        FriendlyName: plugin.Name,
                        PluginDirName: pluginDir,
                        Version: version,
                        Description: plugin.Description,
                        Config: JsonSchema.FromType(plugin.ConfigType, SchemaSettings()),
                        Credentials: JsonSchema.FromType(plugin.CredentialsType, SchemaSettings()),
                        Instance: plugin);

                    var versions = _index.GetOrAdd(plugin.Name, _ =>
                        new SortedDictionary<Version, Entry>(Comparer<Version>.Create((a, b) => b.CompareTo(a))));
                    versions[Version.Parse(version)] = entry;

                    _logger.LogInformation("Cataloged plugin {Name} {Version}", plugin.Name, version);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load plugin from {Dir}/{Version}", pluginDir, version);
                }
            }
        }
    }

    public IReadOnlyList<PluginSummary> ListPlugins() =>
        _index.Select(kvp =>
        {
            var latest = kvp.Value.First().Value;
            return new PluginSummary(kvp.Key, latest.Version, latest.Description);
        }).OrderBy(s => s.Name).ToList();

    public IReadOnlyList<string> ListVersions(string pluginName) =>
        _index.TryGetValue(pluginName, out var versions)
            ? versions.Keys.Select(v => v.ToString()).ToList()
            : Array.Empty<string>();

    public PluginSchema GetSchema(string pluginName, string version)
    {
        var entry = Find(pluginName, version);
        return new PluginSchema(entry.FriendlyName, entry.Version, entry.Description,
            entry.Config, entry.Credentials);
    }

    public IIntegrationPlugin Resolve(string pluginName, string? version)
    {
        var entry = version is null ? FindLatest(pluginName) : Find(pluginName, version);
        return entry.Instance;
    }

    private Entry FindLatest(string pluginName)
    {
        if (!_index.TryGetValue(pluginName, out var versions) || versions.Count == 0)
            throw new KeyNotFoundException($"Plugin '{pluginName}' not found.");
        return versions.First().Value;
    }

    private Entry Find(string pluginName, string version)
    {
        if (!_index.TryGetValue(pluginName, out var versions))
            throw new KeyNotFoundException($"Plugin '{pluginName}' not found.");
        if (!versions.TryGetValue(Version.Parse(version), out var entry))
            throw new KeyNotFoundException($"Plugin '{pluginName}' version '{version}' not found.");
        return entry;
    }

    private static SystemTextJsonSchemaGeneratorSettings SchemaSettings() => new()
    {
        SchemaType = SchemaType.JsonSchema,
        DefaultReferenceTypeNullHandling = ReferenceTypeNullHandling.NotNull,
    };

    private sealed record Entry(
        string FriendlyName,
        string PluginDirName,
        string Version,
        string Description,
        JsonSchema Config,
        JsonSchema Credentials,
        IIntegrationPlugin Instance);
}
```

**Step 2: Run the tests**

Run: `dotnet test tests/IntegrationPro.Application.Tests/IntegrationPro.Application.Tests.csproj`
Expected: PASS.

**Step 3: Commit**

```bash
git add src/IntegrationPro.Application/Catalog/DiskReflectionPluginCatalog.cs \
        tests/IntegrationPro.Application.Tests/
dotnet sln add tests/IntegrationPro.Application.Tests/IntegrationPro.Application.Tests.csproj
git add IntegrationPro.sln
git commit -m "Implement DiskReflectionPluginCatalog with NJsonSchema generation"
```

---

### Task 3.3: Register the catalog in Infrastructure DI

**Files:**
- Modify: `src/IntegrationPro.Infrastructure/DependencyInjection.cs`

**Step 1: Register**

Add inside `AddIntegrationInfrastructure`, immediately after the `PluginLoader` registration:

```csharp
// TODO: replace with NuGetFeedPluginCatalog (lazy pull from feed) when feed-backed
// resolution is ready. Today's disk+reflection impl is fine for the ~dozens-of-plugins scale.
services.AddSingleton<IPluginCatalog>(sp =>
{
    var catalog = new DiskReflectionPluginCatalog(
        sp.GetRequiredService<PluginLoader>(),
        sp.GetRequiredService<ILogger<DiskReflectionPluginCatalog>>());
    catalog.Initialize();
    return catalog;
});
```

Add the matching `using IntegrationPro.Application.Catalog;` at the top.

**Step 2: Build**

Run: `dotnet build IntegrationPro.sln`
Expected: PASS.

**Step 3: Commit**

```bash
git add src/IntegrationPro.Infrastructure/DependencyInjection.cs
git commit -m "Register IPluginCatalog in Infrastructure DI"
```

---

## Phase 4 — `IntegrationPro.Api` project

### Task 4.1: Scaffold the Api project

**Files:**
- Create: `src/IntegrationPro.Api/IntegrationPro.Api.csproj`
- Create: `src/IntegrationPro.Api/Program.cs`
- Create: `src/IntegrationPro.Api/appsettings.json`
- Create: `src/IntegrationPro.Api/appsettings.Development.json`
- Modify: `IntegrationPro.sln`

**Step 1: Project file**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.7.3" />
    <PackageReference Include="NJsonSchema" Version="11.0.2" />
    <PackageReference Include="Microsoft.AspNetCore.WebUtilities" Version="8.0.8" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\IntegrationPro.Application\IntegrationPro.Application.csproj" />
    <ProjectReference Include="..\IntegrationPro.Infrastructure\IntegrationPro.Infrastructure.csproj" />
    <ProjectReference Include="..\IntegrationPro.Domain\IntegrationPro.Domain.csproj" />
    <ProjectReference Include="..\IntegrationPro.PluginBase\IntegrationPro.PluginBase.csproj" />
  </ItemGroup>
</Project>
```

**Step 2: Skeleton `Program.cs`**

```csharp
using IntegrationPro.Api;
using IntegrationPro.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIntegrationInfrastructureForApi(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles();
app.UseDefaultFiles();

PluginEndpoints.Map(app);
IntegrationEndpoints.Map(app);

app.MapHealthChecks("/healthz/live");
app.MapHealthChecks("/healthz/ready");

app.Run();
```

`AddIntegrationInfrastructureForApi` is a new extension; see Task 4.2.

**Step 3: `appsettings.json`**

```json
{
  "Logging": { "LogLevel": { "Default": "Information" } },
  "Kestrel": { "Endpoints": { "Http": { "Url": "http://0.0.0.0:8081" } } },
  "Plugins": { "Directory": "/app/plugins" },
  "Api": {
    "MaxRequestSeconds": 300,
    "BufferThresholdBytes": 30720
  }
}
```

`appsettings.Development.json`:
```json
{
  "Plugins": { "Directory": "plugins-output" }
}
```

**Step 4: Add to solution**

Run: `dotnet sln add src/IntegrationPro.Api/IntegrationPro.Api.csproj`

**Step 5: Build** (will fail on missing types)

Run: `dotnet build src/IntegrationPro.Api/IntegrationPro.Api.csproj`
Expected: FAIL — `PluginEndpoints`, `IntegrationEndpoints`, `AddIntegrationInfrastructureForApi` missing. Proceed.

---

### Task 4.2: Add `AddIntegrationInfrastructureForApi` extension

The Service Bus hosted service belongs only to the Worker. The Api needs everything else.

**Files:**
- Modify: `src/IntegrationPro.Infrastructure/DependencyInjection.cs`

**Step 1: Extract shared registrations**

Refactor to two methods that share a private helper:

```csharp
public static IServiceCollection AddIntegrationInfrastructure(
    this IServiceCollection services, IConfiguration configuration)
{
    AddShared(services, configuration);
    services.AddHostedService<ServiceBusConsumer>();
    return services;
}

public static IServiceCollection AddIntegrationInfrastructureForApi(
    this IServiceCollection services, IConfiguration configuration)
{
    AddShared(services, configuration);
    // No ServiceBusConsumer — Api is HTTP-only.
    return services;
}

private static void AddShared(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<ServiceBusOptions>(configuration.GetSection(ServiceBusOptions.SectionName));

    var pluginsDir = configuration.GetValue<string>("Plugins:Directory") ?? "/app/plugins";
    services.AddSingleton(sp => new PluginLoader(
        pluginsDir, sp.GetRequiredService<ILogger<PluginLoader>>()));

    var outputDir = configuration.GetValue<string>("DataOutput:Directory") ?? "/app/output";
    services.AddSingleton<IDataSaver>(sp => new FileSystemDataSaver(
        outputDir, sp.GetRequiredService<ILogger<FileSystemDataSaver>>()));

    services.AddSingleton<IJobStatusStore, JobStatusStore>();
    services.AddSingleton<LoggingProgressReporter>();
    services.AddSingleton<IProgressReporter>(sp =>
        new HealthTrackingProgressReporter(
            sp.GetRequiredService<LoggingProgressReporter>(),
            sp.GetRequiredService<IJobStatusStore>()));

    services.AddHealthChecks()
        .AddCheck<LivenessHealthCheck>("liveness", tags: new[] { "live" })
        .AddCheck<ReadinessHealthCheck>("readiness", tags: new[] { "ready" });

    services.AddSingleton<IntegrationOrchestrator>();

    // TODO: replace with NuGetFeedPluginCatalog (lazy pull) for 300+ plugins at scale.
    services.AddSingleton<IPluginCatalog>(sp =>
    {
        var catalog = new DiskReflectionPluginCatalog(
            sp.GetRequiredService<PluginLoader>(),
            sp.GetRequiredService<ILogger<DiskReflectionPluginCatalog>>());
        catalog.Initialize();
        return catalog;
    });
}
```

**Step 2: Build**

Run: `dotnet build src/IntegrationPro.Infrastructure/IntegrationPro.Infrastructure.csproj`
Expected: PASS.

---

### Task 4.3: Add DTOs for the Api surface

**Files:**
- Create: `src/IntegrationPro.Api/Contracts/ListPluginsResponse.cs`
- Create: `src/IntegrationPro.Api/Contracts/PluginVersionsResponse.cs`
- Create: `src/IntegrationPro.Api/Contracts/PluginSchemaResponse.cs`
- Create: `src/IntegrationPro.Api/Contracts/RunIntegrationRequest.cs`
- Create: `src/IntegrationPro.Api/Contracts/ErrorResponse.cs`

**Step 1: Create the records**

```csharp
// ListPluginsResponse.cs
namespace IntegrationPro.Api.Contracts;
public sealed record ListPluginsResponse(
    IReadOnlyList<PluginSummaryDto> Items, int Total, int Page, int PageSize);
public sealed record PluginSummaryDto(string Name, string LatestVersion, string Description);

// PluginVersionsResponse.cs
namespace IntegrationPro.Api.Contracts;
public sealed record PluginVersionsResponse(string Name, IReadOnlyList<string> Versions);

// PluginSchemaResponse.cs
namespace IntegrationPro.Api.Contracts;
using System.Text.Json.Nodes;
public sealed record PluginSchemaResponse(
    string Name, string Version, string Description,
    JsonObject Config, JsonObject Credentials);

// RunIntegrationRequest.cs
namespace IntegrationPro.Api.Contracts;
using System.Text.Json.Nodes;
public sealed record RunIntegrationRequest(
    string PluginName,
    string? Version,
    int? TimeoutSeconds,
    JsonObject Credentials,
    JsonObject Configuration);

// ErrorResponse.cs
namespace IntegrationPro.Api.Contracts;
public sealed record ErrorResponse(
    string RequestId,
    string PluginName,
    string? Version,
    string Status,
    ErrorDetail Error);
public sealed record ErrorDetail(string Code, string Message, IReadOnlyList<string>? Details = null);
```

**Step 2: Build**

Run: `dotnet build src/IntegrationPro.Api/IntegrationPro.Api.csproj`
Expected: still fails (endpoints missing). Proceed.

---

### Task 4.4: Write failing tests for discovery endpoints

**Files:**
- Create: `tests/IntegrationPro.Api.Tests/IntegrationPro.Api.Tests.csproj`
- Create: `tests/IntegrationPro.Api.Tests/ApiTestFixture.cs`
- Create: `tests/IntegrationPro.Api.Tests/PluginEndpointsTests.cs`

**Step 1: Project file**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.8" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\IntegrationPro.Api\IntegrationPro.Api.csproj" />
    <ProjectReference Include="..\..\plugins\IntegrationPro.Plugin.Mock\IntegrationPro.Plugin.Mock.csproj">
      <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
    </ProjectReference>
  </ItemGroup>
</Project>
```

**Step 2: Fixture that publishes Mock into a temp plugins dir and boots the host**

```csharp
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

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
    }

    public new Task DisposeAsync()
    {
        try { Directory.Delete(PluginsDir, recursive: true); } catch { }
        return base.DisposeAsync().AsTask();
    }

    protected override IHostBuilder CreateHostBuilder() =>
        base.CreateHostBuilder()!.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Plugins:Directory"] = PluginsDir,
                ["DataOutput:Directory"] = Path.Combine(PluginsDir, "_out"),
            });
        });
}
```

**Step 3: Discovery tests**

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IntegrationPro.Api.Contracts;
using Xunit;

namespace IntegrationPro.Api.Tests;

public sealed class PluginEndpointsTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fx;
    public PluginEndpointsTests(ApiTestFixture fx) => _fx = fx;

    [Fact]
    public async Task GET_plugins_returns_Mock_in_catalog()
    {
        var client = _fx.CreateClient();
        var resp = await client.GetFromJsonAsync<ListPluginsResponse>("/plugins");
        resp!.Items.Should().ContainSingle(i => i.Name == "Mock");
    }

    [Fact]
    public async Task GET_plugin_versions_returns_1_0_0()
    {
        var client = _fx.CreateClient();
        var resp = await client.GetFromJsonAsync<PluginVersionsResponse>("/plugins/Mock/versions");
        resp!.Versions.Should().Equal("1.0.0");
    }

    [Fact]
    public async Task GET_plugin_schema_returns_config_and_credentials()
    {
        var client = _fx.CreateClient();
        var resp = await client.GetAsync("/plugins/Mock/1.0.0/schema");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("companyCount");
        body.Should().Contain("username");
    }

    [Fact]
    public async Task GET_plugin_schema_for_unknown_returns_404()
    {
        var client = _fx.CreateClient();
        var resp = await client.GetAsync("/plugins/DoesNotExist/1.0.0/schema");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

**Step 4: Add to solution + run — expect failure**

```bash
dotnet sln add tests/IntegrationPro.Api.Tests/IntegrationPro.Api.Tests.csproj
dotnet test tests/IntegrationPro.Api.Tests/IntegrationPro.Api.Tests.csproj
```
Expected: FAIL — endpoints don't exist.

---

### Task 4.5: Implement `PluginEndpoints`

**Files:**
- Create: `src/IntegrationPro.Api/Endpoints/PluginEndpoints.cs`

**Step 1: Create the endpoints**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using IntegrationPro.Api.Contracts;
using IntegrationPro.Application.Catalog;

namespace IntegrationPro.Api;

public static class PluginEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/plugins", (IPluginCatalog catalog, int page = 1, int pageSize = 50, string? search = null) =>
        {
            var items = catalog.ListPlugins();
            if (!string.IsNullOrWhiteSpace(search))
                items = items.Where(i => i.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

            var total = items.Count;
            var paged = items.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(i => new PluginSummaryDto(i.Name, i.LatestVersion, i.Description)).ToList();
            return Results.Ok(new ListPluginsResponse(paged, total, page, pageSize));
        });

        app.MapGet("/plugins/{name}/versions", (string name, IPluginCatalog catalog) =>
        {
            var versions = catalog.ListVersions(name);
            if (versions.Count == 0) return Results.NotFound();
            return Results.Ok(new PluginVersionsResponse(name, versions));
        });

        app.MapGet("/plugins/{name}/{version}/schema", (string name, string version, IPluginCatalog catalog) =>
        {
            try
            {
                var schema = catalog.GetSchema(name, version);
                var config = JsonNode.Parse(schema.Config.ToJson())!.AsObject();
                var creds  = JsonNode.Parse(schema.Credentials.ToJson())!.AsObject();
                return Results.Ok(new PluginSchemaResponse(
                    schema.Name, schema.Version, schema.Description, config, creds));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });
    }
}
```

**Step 2: Add temporary no-op for `IntegrationEndpoints` so Program.cs compiles**

Create `src/IntegrationPro.Api/Endpoints/IntegrationEndpoints.cs`:
```csharp
namespace IntegrationPro.Api;
public static class IntegrationEndpoints
{
    public static void Map(WebApplication app) { /* filled in Task 4.7 */ }
}
```

**Step 3: Run tests**

Run: `dotnet test tests/IntegrationPro.Api.Tests/IntegrationPro.Api.Tests.csproj`
Expected: PASS.

**Step 4: Commit**

```bash
git add src/IntegrationPro.Api/ src/IntegrationPro.Infrastructure/DependencyInjection.cs \
        tests/IntegrationPro.Api.Tests/ IntegrationPro.sln
git commit -m "Add IntegrationPro.Api project with discovery endpoints"
```

---

### Task 4.6: Write failing tests for the execution endpoint

**Files:**
- Create: `tests/IntegrationPro.Api.Tests/IntegrationEndpointsTests.cs`

**Step 1: Test happy path + validation failures**

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using IntegrationPro.Api.Contracts;
using Xunit;

namespace IntegrationPro.Api.Tests;

public sealed class IntegrationEndpointsTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fx;
    public IntegrationEndpointsTests(ApiTestFixture fx) => _fx = fx;

    [Fact]
    public async Task POST_run_returns_the_mock_data_stream_as_json()
    {
        var client = _fx.CreateClient();
        var req = new RunIntegrationRequest(
            PluginName: "Mock", Version: "1.0.0", TimeoutSeconds: 30,
            Credentials: new JsonObject { ["username"] = "u", ["password"] = "p" },
            Configuration: new JsonObject { ["companyCount"] = 3, ["delayMs"] = 0 });

        var resp = await client.PostAsJsonAsync("/integrations/run", req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        resp.Headers.Should().Contain(h => h.Key == "X-Request-Id");
        resp.Headers.Should().Contain(h => h.Key == "X-Plugin-Version");

        var body = await resp.Content.ReadAsStringAsync();
        var arr = JsonNode.Parse(body)!.AsArray();
        arr.Count.Should().Be(3);
    }

    [Fact]
    public async Task POST_run_with_missing_required_credential_returns_400()
    {
        var client = _fx.CreateClient();
        var req = new RunIntegrationRequest(
            PluginName: "Mock", Version: "1.0.0", TimeoutSeconds: 10,
            Credentials: new JsonObject(),                 // missing username/password
            Configuration: new JsonObject { ["companyCount"] = 1 });

        var resp = await client.PostAsJsonAsync("/integrations/run", req);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_run_for_unknown_plugin_returns_404()
    {
        var client = _fx.CreateClient();
        var req = new RunIntegrationRequest("DoesNotExist", null, 10,
            new JsonObject(), new JsonObject());
        var resp = await client.PostAsJsonAsync("/integrations/run", req);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_run_with_SimulateFailure_returns_500_with_error_body()
    {
        var client = _fx.CreateClient();
        var req = new RunIntegrationRequest(
            PluginName: "Mock", Version: "1.0.0", TimeoutSeconds: 10,
            Credentials: new JsonObject { ["username"] = "u", ["password"] = "p" },
            Configuration: new JsonObject { ["companyCount"] = 4, ["simulateFailure"] = true, ["delayMs"] = 0 });

        var resp = await client.PostAsJsonAsync("/integrations/run", req);
        resp.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Status.Should().Be("Failed");
        err.Error.Message.Should().Contain("Simulated");
    }
}
```

**Step 2: Run tests — expect all four to fail (endpoint returns 404 because it isn't wired)**

Run: `dotnet test tests/IntegrationPro.Api.Tests/IntegrationPro.Api.Tests.csproj --filter IntegrationEndpointsTests`
Expected: FAIL.

---

### Task 4.7: Implement the execution endpoint

**Files:**
- Create: `src/IntegrationPro.Api/Execution/SyncDataSaver.cs`
- Create: `src/IntegrationPro.Api/Execution/SyncExecutor.cs`
- Modify: `src/IntegrationPro.Api/Endpoints/IntegrationEndpoints.cs`

**Step 1: `SyncDataSaver` — single-emission into a `FileBufferingWriteStream`**

```csharp
using Microsoft.AspNetCore.WebUtilities;
using IntegrationPro.Application.Interfaces;

namespace IntegrationPro.Api.Execution;

/// <summary>
/// Captures a single OnDataReady emission into a FileBufferingWriteStream so the
/// outer pipeline can decide between flushing (success) and discarding (error).
/// Throws on any second emission — sync mode is single-emission only.
/// </summary>
internal sealed class SyncDataSaver : IDataSaver
{
    // Buffered to allow proper 4xx/5xx error responses on mid-run failures.
    // Optimization: swap for direct pipe-through (plugin stream -> response.Body)
    // if streaming semantics and lower TTFB become more important than clean
    // error status codes.
    public FileBufferingWriteStream Buffer { get; } = new(memoryThreshold: 30 * 1024);

    public string? DataType { get; private set; }
    public bool HasEmitted => DataType is not null;

    public async Task SaveAsync(string requestId, string dataType, Stream data, CancellationToken ct = default)
    {
        if (HasEmitted)
            throw new InvalidOperationException(
                "Sync mode allows a single OnDataReady emission. Use the Service Bus path for multi-emission plugins.");
        DataType = dataType;
        await data.CopyToAsync(Buffer, ct);
    }
}
```

**Step 2: `SyncExecutor` — validate + flatten + run + shape response**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using IntegrationPro.Api.Contracts;
using IntegrationPro.Application.Catalog;
using IntegrationPro.Application.Interfaces;
using IntegrationPro.Application.Services;
using IntegrationPro.Domain.Messages;
using Microsoft.Extensions.Logging;
using NJsonSchema.Validation;

namespace IntegrationPro.Api.Execution;

public sealed class SyncExecutor
{
    private readonly IPluginCatalog _catalog;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IProgressReporter _progressReporter;
    private readonly ILogger<SyncExecutor> _logger;

    public SyncExecutor(IPluginCatalog catalog, ILoggerFactory loggerFactory,
                       IProgressReporter progressReporter, ILogger<SyncExecutor> logger)
    { _catalog = catalog; _loggerFactory = loggerFactory; _progressReporter = progressReporter; _logger = logger; }

    public async Task<SyncResult> ExecuteAsync(
        RunIntegrationRequest request, HttpContext http, CancellationToken ct)
    {
        var requestId = Guid.NewGuid().ToString("N");

        PluginSchema schema;
        try { schema = _catalog.GetSchema(request.PluginName, request.Version ?? FirstVersion(request.PluginName)); }
        catch (KeyNotFoundException) { return SyncResult.NotFound(requestId, request); }

        var credErrors = schema.Credentials.Validate(request.Credentials.ToJsonString());
        var cfgErrors  = schema.Config.Validate(request.Configuration.ToJsonString());
        if (credErrors.Count > 0 || cfgErrors.Count > 0)
            return SyncResult.Validation(requestId, request,
                credErrors.Select(e => "credentials: " + e).Concat(cfgErrors.Select(e => "configuration: " + e)).ToList());

        var plugin = _catalog.Resolve(request.PluginName, request.Version);

        var message = BuildMessage(requestId, plugin.Name, plugin.Version, request);
        var saver = new SyncDataSaver();
        var orchestrator = new IntegrationOrchestrator(
            pluginLoader: null!,                 // orchestrator reloads; but we're re-using cached instance — see note below
            dataSaver: saver,
            progressReporter: _progressReporter,
            loggerFactory: _loggerFactory,
            logger: _loggerFactory.CreateLogger<IntegrationOrchestrator>());

        // Note: today's IntegrationOrchestrator loads the plugin itself via PluginLoader.
        // For sync mode we short-circuit by invoking the plugin directly with the built context
        // so the cached catalog instance is used (avoiding a second load). The orchestrator
        // path stays intact for the ServiceBus flow.
        var context = SyncContextBuilder.Build(plugin, message, saver, _progressReporter, _loggerFactory, ct);
        try
        {
            await plugin.InitializeAsync(context);
            await plugin.ExecuteAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return SyncResult.Timeout(requestId, request);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Sync mode allows"))
        {
            return SyncResult.MultiEmission(requestId, request, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin execution failed for {RequestId}", requestId);
            return SyncResult.Failed(requestId, request, ex.Message);
        }
        finally
        {
            try { await plugin.ShutdownAsync(); } catch (Exception ex)
            { _logger.LogWarning(ex, "Shutdown failed for {RequestId}", requestId); }
        }

        return SyncResult.Ok(requestId, request, plugin.Version, saver);
    }

    private string FirstVersion(string name) =>
        _catalog.ListVersions(name).FirstOrDefault()
            ?? throw new KeyNotFoundException($"Plugin '{name}' not found.");

    private static IntegrationRequestMessage BuildMessage(string requestId, string name, string version, RunIntegrationRequest r)
    {
        var credUsername = r.Credentials["username"]?.GetValue<string>() ?? "";
        var credPassword = r.Credentials["password"]?.GetValue<string>() ?? "";
        var additional = r.Credentials
            .Where(kv => kv.Key is not ("username" or "password"))
            .ToDictionary(kv => UpperFirst(kv.Key), kv => kv.Value?.ToString() ?? "");

        var cfg = r.Configuration.ToDictionary(kv => UpperFirst(kv.Key), kv => kv.Value?.ToString() ?? "");

        return new IntegrationRequestMessage
        {
            RequestId = requestId,
            PluginName = name,
            Credentials = new MessageCredentials
            {
                Username = credUsername, Password = credPassword,
                AdditionalFields = additional
            },
            Configuration = cfg
        };
    }

    private static string UpperFirst(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}
```

**Step 3: `SyncContextBuilder` — wires `PluginContext` with the sync-mode callbacks**

Create `src/IntegrationPro.Api/Execution/SyncContextBuilder.cs`:
```csharp
using IntegrationPro.Application.Interfaces;
using IntegrationPro.Domain.Messages;
using IntegrationPro.PluginBase;
using Microsoft.Extensions.Logging;

namespace IntegrationPro.Api.Execution;

internal static class SyncContextBuilder
{
    public static PluginContext Build(
        IIntegrationPlugin plugin,
        IntegrationRequestMessage msg,
        SyncDataSaver saver,
        IProgressReporter progress,
        ILoggerFactory loggerFactory,
        CancellationToken ct) => new()
    {
        RequestId = msg.RequestId,
        Credentials = new PluginCredentials
        {
            Username = msg.Credentials.Username, Password = msg.Credentials.Password,
            AdditionalFields = msg.Credentials.AdditionalFields
        },
        Configuration = msg.Configuration,
        Logger = loggerFactory.CreateLogger($"Plugin.{plugin.Name}"),
        OnStarted   = d => progress.ReportStartedAsync(msg.RequestId, plugin.Name, d),
        OnProgress  = (c, t, d) => progress.ReportProgressAsync(msg.RequestId, c, t, d),
        OnDataReady = (type, stream) => saver.SaveAsync(msg.RequestId, type, stream, ct),
        OnFailed    = (m, ex) => progress.ReportFailedAsync(msg.RequestId, m, ex),
        OnCompleted = s => progress.ReportCompletedAsync(msg.RequestId, s),
    };
}
```

**Step 4: `SyncResult` union**

Create `src/IntegrationPro.Api/Execution/SyncResult.cs`:
```csharp
using IntegrationPro.Api.Contracts;

namespace IntegrationPro.Api.Execution;

public abstract record SyncResult
{
    public sealed record Success(string RequestId, string PluginName, string Version, SyncDataSaver Saver) : SyncResult;
    public sealed record NotFoundErr(ErrorResponse Body) : SyncResult;
    public sealed record ValidationErr(ErrorResponse Body) : SyncResult;
    public sealed record TimeoutErr(ErrorResponse Body) : SyncResult;
    public sealed record MultiEmissionErr(ErrorResponse Body) : SyncResult;
    public sealed record FailedErr(ErrorResponse Body) : SyncResult;

    public static SyncResult Ok(string id, RunIntegrationRequest r, string v, SyncDataSaver s) =>
        new Success(id, r.PluginName, v, s);
    public static SyncResult NotFound(string id, RunIntegrationRequest r) =>
        new NotFoundErr(new ErrorResponse(id, r.PluginName, r.Version, "Failed",
            new ErrorDetail("plugin_not_found", $"Plugin '{r.PluginName}' or version '{r.Version}' not found.")));
    public static SyncResult Validation(string id, RunIntegrationRequest r, IReadOnlyList<string> details) =>
        new ValidationErr(new ErrorResponse(id, r.PluginName, r.Version, "Failed",
            new ErrorDetail("validation_failed", "Request body does not match plugin schema.", details)));
    public static SyncResult Timeout(string id, RunIntegrationRequest r) =>
        new TimeoutErr(new ErrorResponse(id, r.PluginName, r.Version, "Failed",
            new ErrorDetail("timeout", "Plugin execution exceeded the allowed duration.")));
    public static SyncResult MultiEmission(string id, RunIntegrationRequest r, string msg) =>
        new MultiEmissionErr(new ErrorResponse(id, r.PluginName, r.Version, "Failed",
            new ErrorDetail("multi_emission", msg)));
    public static SyncResult Failed(string id, RunIntegrationRequest r, string msg) =>
        new FailedErr(new ErrorResponse(id, r.PluginName, r.Version, "Failed",
            new ErrorDetail("plugin_failure", msg)));
}
```

**Step 5: Wire into `IntegrationEndpoints`**

Replace the stub body of `src/IntegrationPro.Api/Endpoints/IntegrationEndpoints.cs`:
```csharp
using IntegrationPro.Api.Contracts;
using IntegrationPro.Api.Execution;
using Microsoft.AspNetCore.WebUtilities;

namespace IntegrationPro.Api;

public static class IntegrationEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/integrations/run", async (
            RunIntegrationRequest request,
            SyncExecutor executor,
            HttpContext http,
            IConfiguration config) =>
        {
            var maxSeconds = config.GetValue<int?>("Api:MaxRequestSeconds") ?? 300;
            var clientSeconds = request.TimeoutSeconds ?? maxSeconds;
            var effectiveSeconds = Math.Min(clientSeconds, maxSeconds);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
            cts.CancelAfter(TimeSpan.FromSeconds(effectiveSeconds));

            var result = await executor.ExecuteAsync(request, http, cts.Token);

            switch (result)
            {
                case SyncResult.Success s:
                    http.Response.StatusCode = StatusCodes.Status200OK;
                    http.Response.ContentType = "application/json";
                    http.Response.Headers["X-Request-Id"] = s.RequestId;
                    http.Response.Headers["X-Plugin-Version"] = s.Version;
                    await s.Saver.Buffer.DrainBufferAsync(http.Response.Body, http.RequestAborted);
                    return Results.Empty;
                case SyncResult.NotFoundErr n:
                    return Results.Json(n.Body, statusCode: StatusCodes.Status404NotFound);
                case SyncResult.ValidationErr v:
                    return Results.Json(v.Body, statusCode: StatusCodes.Status400BadRequest);
                case SyncResult.TimeoutErr t:
                    return Results.Json(t.Body, statusCode: StatusCodes.Status408RequestTimeout);
                case SyncResult.MultiEmissionErr m:
                    return Results.Json(m.Body, statusCode: StatusCodes.Status409Conflict);
                case SyncResult.FailedErr f:
                    return Results.Json(f.Body, statusCode: StatusCodes.Status500InternalServerError);
                default:
                    return Results.StatusCode(500);
            }
        });
    }
}
```

**Step 6: Register `SyncExecutor` in DI**

Modify `src/IntegrationPro.Api/Program.cs` — add immediately after `AddIntegrationInfrastructureForApi`:
```csharp
builder.Services.AddSingleton<IntegrationPro.Api.Execution.SyncExecutor>();
```

**Step 7: Run tests**

Run: `dotnet test tests/IntegrationPro.Api.Tests/IntegrationPro.Api.Tests.csproj`
Expected: PASS.

**Step 8: Commit**

```bash
git add src/IntegrationPro.Api/Execution/ src/IntegrationPro.Api/Endpoints/IntegrationEndpoints.cs \
        src/IntegrationPro.Api/Program.cs tests/IntegrationPro.Api.Tests/IntegrationEndpointsTests.cs
git commit -m "Add synchronous /integrations/run endpoint with buffered output"
```

---

### Task 4.8: Manual smoke test via Swagger UI

**Files:** none.

**Step 1: Publish the Mock plugin**

```bash
rm -rf plugins-output
dotnet publish plugins/IntegrationPro.Plugin.Mock/IntegrationPro.Plugin.Mock.csproj -c Release \
    -o ./plugins-output/IntegrationPro.Plugin.Mock/1.0.0
```

**Step 2: Run the Api**

```bash
dotnet run --project src/IntegrationPro.Api/IntegrationPro.Api.csproj
```

**Step 3: Open http://localhost:8081/swagger**

Verify:
- Four operations listed: `GET /plugins`, `GET /plugins/{name}/versions`, `GET /plugins/{name}/{version}/schema`, `POST /integrations/run`.
- "Try it out" on `GET /plugins` returns Mock.
- "Try it out" on `POST /integrations/run` with the Mock body from Task 4.6 returns a JSON array of mock companies.
- `X-Request-Id` and `X-Plugin-Version` headers present in the response.

**Step 4: No commit** — this is manual verification.

---

## Phase 5 — Playground UI

### Task 5.1: Scaffold the Vite + React + TS project

**Files:**
- Create: `src/IntegrationPro.Api.Ui/package.json`
- Create: `src/IntegrationPro.Api.Ui/tsconfig.json`
- Create: `src/IntegrationPro.Api.Ui/vite.config.ts`
- Create: `src/IntegrationPro.Api.Ui/index.html`
- Create: `src/IntegrationPro.Api.Ui/src/main.tsx`
- Create: `src/IntegrationPro.Api.Ui/src/App.tsx`
- Create: `src/IntegrationPro.Api.Ui/.gitignore`

**Step 1: `package.json`**

```json
{
  "name": "integrationpro-api-ui",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc && vite build",
    "preview": "vite preview"
  },
  "dependencies": {
    "@rjsf/core": "^5.18.0",
    "@rjsf/utils": "^5.18.0",
    "@rjsf/validator-ajv8": "^5.18.0",
    "react": "^18.3.1",
    "react-dom": "^18.3.1"
  },
  "devDependencies": {
    "@types/react": "^18.3.3",
    "@types/react-dom": "^18.3.0",
    "@vitejs/plugin-react": "^4.3.1",
    "typescript": "^5.5.3",
    "vite": "^5.4.1"
  }
}
```

**Step 2: `vite.config.ts`**

```ts
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  base: "/ui/",
  plugins: [react()],
  server: { proxy: { "/plugins": "http://localhost:8081", "/integrations": "http://localhost:8081" } },
  build: { outDir: "dist", emptyOutDir: true }
});
```

**Step 3: `tsconfig.json`** — standard Vite React template (omitted for brevity; see a fresh `npm create vite@latest` output).

**Step 4: `index.html`**

```html
<!doctype html>
<html><head><meta charset="UTF-8"><title>IntegrationPro Playground</title></head>
<body><div id="root"></div><script type="module" src="/src/main.tsx"></script></body></html>
```

**Step 5: `main.tsx`**

```tsx
import React from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
createRoot(document.getElementById("root")!).render(<App />);
```

**Step 6: `App.tsx` skeleton — filled in next task**

```tsx
export function App() { return <div>Loading…</div>; }
```

**Step 7: `.gitignore`**

```
node_modules/
dist/
```

**Step 8: Verify install/build**

```bash
cd src/IntegrationPro.Api.Ui && npm install && npm run build && cd ../../..
```
Expected: PASS; `src/IntegrationPro.Api.Ui/dist/` exists with an `index.html` inside.

**Step 9: Commit**

```bash
git add src/IntegrationPro.Api.Ui/package.json src/IntegrationPro.Api.Ui/tsconfig.json \
        src/IntegrationPro.Api.Ui/vite.config.ts src/IntegrationPro.Api.Ui/index.html \
        src/IntegrationPro.Api.Ui/src/ src/IntegrationPro.Api.Ui/.gitignore
git commit -m "Scaffold IntegrationPro.Api.Ui Vite+React project"
```

---

### Task 5.2: Implement the playground flow

**Files:**
- Modify: `src/IntegrationPro.Api.Ui/src/App.tsx`
- Create: `src/IntegrationPro.Api.Ui/src/PluginPicker.tsx`
- Create: `src/IntegrationPro.Api.Ui/src/RunForm.tsx`
- Create: `src/IntegrationPro.Api.Ui/src/ResponseView.tsx`

**Step 1: `PluginPicker.tsx` — paginated list + search + version dropdown**

```tsx
import { useEffect, useState } from "react";

type Summary = { name: string; latestVersion: string; description: string };

export function PluginPicker({ onSelect }: { onSelect: (name: string, version: string) => void }) {
  const [items, setItems] = useState<Summary[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [selectedName, setSelectedName] = useState<string | null>(null);
  const [versions, setVersions] = useState<string[]>([]);
  const [selectedVersion, setSelectedVersion] = useState<string | null>(null);

  useEffect(() => {
    fetch(`/plugins?page=${page}&pageSize=50&search=${encodeURIComponent(search)}`)
      .then(r => r.json()).then(b => { setItems(b.items); setTotal(b.total); });
  }, [page, search]);

  useEffect(() => {
    if (!selectedName) return;
    fetch(`/plugins/${selectedName}/versions`)
      .then(r => r.json()).then(b => { setVersions(b.versions); setSelectedVersion(b.versions[0] ?? null); });
  }, [selectedName]);

  useEffect(() => {
    if (selectedName && selectedVersion) onSelect(selectedName, selectedVersion);
  }, [selectedName, selectedVersion, onSelect]);

  return (
    <div>
      <input placeholder="search" value={search} onChange={e => { setPage(1); setSearch(e.target.value); }} />
      <ul>
        {items.map(i => (
          <li key={i.name}>
            <button onClick={() => setSelectedName(i.name)}>
              {i.name} <small>{i.latestVersion}</small> — {i.description}
            </button>
          </li>
        ))}
      </ul>
      <div>Page {page} / {Math.max(1, Math.ceil(total / 50))}</div>
      <button disabled={page === 1} onClick={() => setPage(p => p - 1)}>Prev</button>
      <button disabled={page * 50 >= total} onClick={() => setPage(p => p + 1)}>Next</button>
      {selectedName && (
        <select value={selectedVersion ?? ""} onChange={e => setSelectedVersion(e.target.value)}>
          {versions.map(v => <option key={v} value={v}>{v}</option>)}
        </select>
      )}
    </div>
  );
}
```

**Step 2: `RunForm.tsx` — two RJSF forms side by side**

```tsx
import { useEffect, useState } from "react";
import Form from "@rjsf/core";
import validator from "@rjsf/validator-ajv8";

export function RunForm({ name, version, onResult }: {
  name: string; version: string; onResult: (resp: Response) => void;
}) {
  const [schema, setSchema] = useState<any | null>(null);
  const [credentials, setCredentials] = useState<any>({});
  const [configuration, setConfiguration] = useState<any>({});
  const [pending, setPending] = useState(false);
  const [abort, setAbort] = useState<AbortController | null>(null);

  useEffect(() => {
    fetch(`/plugins/${name}/${version}/schema`).then(r => r.json()).then(setSchema);
  }, [name, version]);

  async function run() {
    const ac = new AbortController();
    setAbort(ac); setPending(true);
    try {
      const resp = await fetch("/integrations/run", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ pluginName: name, version, credentials, configuration }),
        signal: ac.signal,
      });
      onResult(resp);
    } finally { setPending(false); setAbort(null); }
  }

  if (!schema) return <div>Loading schema…</div>;
  return (
    <div style={{ display: "flex", gap: 16 }}>
      <div>
        <h3>Credentials</h3>
        <Form schema={schema.credentials} validator={validator} formData={credentials}
              onChange={e => setCredentials(e.formData)} uiSchema={{ password: { "ui:widget": "password" } }}>
          <span/>
        </Form>
      </div>
      <div>
        <h3>Configuration</h3>
        <Form schema={schema.config} validator={validator} formData={configuration}
              onChange={e => setConfiguration(e.formData)}>
          <span/>
        </Form>
      </div>
      <div>
        <button onClick={run} disabled={pending}>Run</button>
        {pending && <button onClick={() => abort?.abort()}>Cancel</button>}
      </div>
    </div>
  );
}
```

**Step 3: `ResponseView.tsx` — JSON/CSV/error rendering**

```tsx
import { useEffect, useState } from "react";

export function ResponseView({ resp }: { resp: Response | null }) {
  const [body, setBody] = useState<string>("");
  useEffect(() => { resp?.clone().text().then(setBody); }, [resp]);
  if (!resp) return null;
  const ct = resp.headers.get("content-type") ?? "";
  if (resp.status >= 400) {
    return <pre style={{ background: "#fee" }}>{body}</pre>;
  }
  if (ct.includes("application/json")) {
    try { return <pre>{JSON.stringify(JSON.parse(body), null, 2)}</pre>; } catch { return <pre>{body}</pre>; }
  }
  if (ct.includes("text/csv")) {
    const rows = body.split("\n").slice(0, 101);
    return <pre>{rows.join("\n")}</pre>;
  }
  return <a href={URL.createObjectURL(new Blob([body]))} download>Download</a>;
}
```

**Step 4: `App.tsx`**

```tsx
import { useState } from "react";
import { PluginPicker } from "./PluginPicker";
import { RunForm } from "./RunForm";
import { ResponseView } from "./ResponseView";

export function App() {
  const [selection, setSelection] = useState<{ name: string; version: string } | null>(null);
  const [resp, setResp] = useState<Response | null>(null);
  return (
    <div style={{ fontFamily: "system-ui", padding: 16 }}>
      <h1>IntegrationPro Playground</h1>
      <PluginPicker onSelect={(name, version) => setSelection({ name, version })} />
      {selection && <RunForm name={selection.name} version={selection.version} onResult={setResp} />}
      <ResponseView resp={resp} />
    </div>
  );
}
```

**Step 5: Build UI standalone**

```bash
cd src/IntegrationPro.Api.Ui && npm run build && cd ../../..
```
Expected: PASS; `dist/` rebuilt.

**Step 6: Commit**

```bash
git add src/IntegrationPro.Api.Ui/src/
git commit -m "Add playground flow: picker, form, response view"
```

---

### Task 5.3: Wire UI build into `IntegrationPro.Api.csproj`

**Files:**
- Modify: `src/IntegrationPro.Api/IntegrationPro.Api.csproj`

**Step 1: Add MSBuild target**

Append before the closing `</Project>`:

```xml
<Target Name="BuildPlaygroundUi" BeforeTargets="Build">
  <PropertyGroup>
    <UiDir>$(MSBuildThisFileDirectory)..\IntegrationPro.Api.Ui</UiDir>
    <UiOut>$(MSBuildThisFileDirectory)wwwroot\ui</UiOut>
  </PropertyGroup>
  <Exec Command="npm ci" WorkingDirectory="$(UiDir)" Condition="!Exists('$(UiDir)\node_modules')" />
  <Exec Command="npm run build" WorkingDirectory="$(UiDir)" />
  <ItemGroup>
    <UiBuiltFiles Include="$(UiDir)\dist\**\*" />
  </ItemGroup>
  <RemoveDir Directories="$(UiOut)" Condition="Exists('$(UiOut)')" />
  <MakeDir Directories="$(UiOut)" />
  <Copy SourceFiles="@(UiBuiltFiles)" DestinationFiles="@(UiBuiltFiles->'$(UiOut)\%(RecursiveDir)%(Filename)%(Extension)')" />
</Target>
```

Also add an ignore for the generated folder (create `src/IntegrationPro.Api/.gitignore` if missing):
```
wwwroot/ui/
```

**Step 2: Full rebuild**

```bash
dotnet build src/IntegrationPro.Api/IntegrationPro.Api.csproj
```
Expected: PASS. `src/IntegrationPro.Api/wwwroot/ui/index.html` now exists.

**Step 3: Run the Api and verify playground**

```bash
dotnet run --project src/IntegrationPro.Api/IntegrationPro.Api.csproj
```
Open http://localhost:8081/ui/ — verify the Mock plugin appears, schemas render, and `Run` posts to `/integrations/run` and shows the JSON array.

**Step 4: Commit**

```bash
git add src/IntegrationPro.Api/IntegrationPro.Api.csproj src/IntegrationPro.Api/.gitignore
git commit -m "Build UI into Api wwwroot via MSBuild"
```

---

## Phase 6 — E2E validation + docs + Api Docker image

### Task 6.1: Add the Api to the Docker build

**Files:**
- Create: `src/IntegrationPro.Api/Dockerfile`

**Step 1: New Dockerfile for the Api**

```dockerfile
FROM node:20-alpine AS ui
WORKDIR /ui
COPY src/IntegrationPro.Api.Ui/ ./
RUN npm ci && npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY IntegrationPro.sln .
COPY src/ src/
COPY plugins/ plugins/
COPY --from=ui /ui/dist/ src/IntegrationPro.Api/wwwroot/ui/
RUN dotnet publish src/IntegrationPro.Api/IntegrationPro.Api.csproj -c Release -o /app/publish /p:SkipUiBuild=true
RUN dotnet publish plugins/IntegrationPro.Plugin.PrismHR/IntegrationPro.Plugin.PrismHR.csproj -c Release -o /app/plugins/IntegrationPro.Plugin.PrismHR/1.0.0
RUN dotnet publish plugins/IntegrationPro.Plugin.Mock/IntegrationPro.Plugin.Mock.csproj         -c Release -o /app/plugins/IntegrationPro.Plugin.Mock/1.0.0

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/plugins ./plugins
EXPOSE 8081
ENTRYPOINT ["dotnet", "IntegrationPro.Api.dll"]
```

**Step 2: Gate the MSBuild UI target on `SkipUiBuild`**

In `IntegrationPro.Api.csproj`, change the target's attributes:
```xml
<Target Name="BuildPlaygroundUi" BeforeTargets="Build" Condition="'$(SkipUiBuild)' != 'true'">
```

**Step 3: Build the image**

```bash
docker build -t integrationpro-api -f src/IntegrationPro.Api/Dockerfile .
```
Expected: image builds successfully.

**Step 4: Commit**

```bash
git add src/IntegrationPro.Api/Dockerfile src/IntegrationPro.Api/IntegrationPro.Api.csproj
git commit -m "Add Dockerfile for IntegrationPro.Api"
```

---

### Task 6.2: Update `README.md` + `CLAUDE.md`

**Files:**
- Modify: `README.md`
- Modify: `CLAUDE.md`

**Step 1: `README.md`** — add a "Synchronous API" section documenting:

- How to run locally: `dotnet run --project src/IntegrationPro.Api/IntegrationPro.Api.csproj`.
- Default ports: Worker 8080 (health only), Api 8081.
- Swagger at `/swagger`, playground at `/ui/`.
- The four endpoints: `GET /plugins`, `GET /plugins/{name}/versions`, `GET /plugins/{name}/{version}/schema`, `POST /integrations/run`.
- Link to the design doc: `docs/plans/2026-04-21-sync-integration-api-design.md`.

**Step 2: `CLAUDE.md`**

Add in the Architecture section:
- A bullet under "Projects": `IntegrationPro.Api` — ASP.NET host for synchronous HTTP execution, discovery endpoints, Swagger UI, and the playground UI. Shares `Application` and `Infrastructure` with Worker.
- A bullet under "Configuration": `Api:MaxRequestSeconds` and `Api:BufferThresholdBytes`.

Add a new "Build Commands" entry:
```bash
# Run the synchronous API (with Swagger UI + playground)
dotnet run --project src/IntegrationPro.Api/IntegrationPro.Api.csproj

# Build the Api Docker image
docker build -t integrationpro-api -f src/IntegrationPro.Api/Dockerfile .
```

**Step 3: Commit**

```bash
git add README.md CLAUDE.md
git commit -m "Document IntegrationPro.Api usage"
```

---

### Task 6.3: Full-solution test sweep

**Files:** none.

**Step 1: Run every test project**

```bash
dotnet test IntegrationPro.sln
```
Expected: all tests PASS.

**Step 2: Build the solution cleanly**

```bash
dotnet clean IntegrationPro.sln
dotnet build IntegrationPro.sln
```
Expected: PASS, no warnings in new projects.

**Step 3: Run both hosts side-by-side manually**

- Terminal 1: `dotnet run --project src/IntegrationPro.Worker/IntegrationPro.Worker.csproj` — verify `/healthz/live` still returns 200.
- Terminal 2: `dotnet run --project src/IntegrationPro.Api/IntegrationPro.Api.csproj` — verify `/swagger` and `/ui/` work; run the Mock plugin end-to-end.

**Step 4: No commit** — verification only.

---

## Out of scope / future work (already in the design doc)

- `NuGetFeedPluginCatalog` (lazy pull from a feed at request time).
- Auth (API key / OAuth middleware).
- `GET /jobs/{requestId}` read-side for Service-Bus-initiated jobs.
- Direct stream pipe-through (skip the buffer) when TTFB matters more than clean error status codes.
- Hot plugin reload.
- Streaming progress via HTTP/2 trailers or SSE.

---

## Completion checklist

- [ ] Phase 1: plugin contract + Mock & PrismHR migrated, build green.
- [ ] Phase 2: versioned directory layout, PluginLoader updated, TestHarness green, Dockerfile updated.
- [ ] Phase 3: `IPluginCatalog` + `DiskReflectionPluginCatalog` + unit tests green.
- [ ] Phase 4: `IntegrationPro.Api` project with discovery + execution endpoints, all `IntegrationPro.Api.Tests` green, manual Swagger smoke clean.
- [ ] Phase 5: Playground UI building into `wwwroot/ui`, manual playground smoke clean.
- [ ] Phase 6: Api Docker image builds; README/CLAUDE.md updated; full-solution test sweep green; Worker + Api manually verified running side by side.

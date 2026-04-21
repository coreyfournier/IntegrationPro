# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build entire solution
dotnet build IntegrationPro.sln

# Build specific project
dotnet build src/IntegrationPro.Worker/IntegrationPro.Worker.csproj

# Run the worker locally
dotnet run --project src/IntegrationPro.Worker/IntegrationPro.Worker.csproj

# Publish plugins (each plugin must be published to its own directory)
dotnet publish plugins/IntegrationPro.Plugin.PrismHR/IntegrationPro.Plugin.PrismHR.csproj -c Release -o ./plugins-output/IntegrationPro.Plugin.PrismHR/1.0.0
dotnet publish plugins/IntegrationPro.Plugin.Mock/IntegrationPro.Plugin.Mock.csproj       -c Release -o ./plugins-output/IntegrationPro.Plugin.Mock/1.0.0

# Docker build
docker build -t integrationpro .
```

## Architecture

This is a containerized Azure App Job (C#/.NET 8) following **Domain-Driven Design**, event-driven from **Azure Service Bus**. Its purpose is **ETL (Extract Transform Load)** — pull data from external systems via plugins, transform it into a common model, and save it to a destination.

### Project Dependency Flow

```
Worker → Infrastructure → Application → Domain
                                      → PluginBase
Plugins (loaded at runtime) → PluginBase
```

### Projects

- **IntegrationPro.PluginBase** — Shared plugin contracts (`IIntegrationPlugin`, `PluginContext`, `PluginCredentials`). Referenced by plugins with `<Private>false</Private>` and `<ExcludeAssets>runtime</ExcludeAssets>` per .NET dynamic loading requirements.
- **IntegrationPro.Domain** — Domain entities (`IntegrationJob`, `JobProgressEntry`), value objects, enums (`JobStatus`), Service Bus message models (`IntegrationRequestMessage`), and the common data model (`CommonCompanyData`).
- **IntegrationPro.Application** — Plugin loading (`PluginLoader`, `PluginLoadContext` via `AssemblyLoadContext`), orchestration (`IntegrationOrchestrator`), and interfaces (`IDataSaver`, `IProgressReporter`).
- **IntegrationPro.Infrastructure** — Implementations: `ServiceBusConsumer` (hosted service listening to queue), `FileSystemDataSaver`, `LoggingProgressReporter`, DI registration (`DependencyInjection.AddIntegrationInfrastructure`).
- **IntegrationPro.Worker** — Entry point. Minimal `Program.cs` that wires up infrastructure DI and starts the host.

### Plugin System

Plugins are loaded at runtime using .NET's native plugin architecture (`AssemblyLoadContext` + `AssemblyDependencyResolver`). Each plugin gets its own load context for dependency isolation.

**Plugin contract** (`IIntegrationPlugin`):
1. `InitializeAsync(PluginContext)` — Receive credentials/config, authenticate with target system
2. `ExecuteAsync(CancellationToken)` — Perform extraction and transformation, using callbacks on `PluginContext`
3. `ShutdownAsync()` — Clean up (logout, close connections)

**PluginContext callbacks** (all `Func<..., Task>`):
- `OnStarted` — Report extraction beginning
- `OnProgress(currentStep, totalSteps, description)` — Report progress
- `OnDataReady(dataType, Stream)` — Deliver transformed data as a generic stream (not a model)
- `OnFailed(errorMessage, exception)` — Report failures
- `OnCompleted(summary)` — Report successful completion

**Adding a new plugin**: Create a class library in `plugins/`, set `<EnableDynamicLoading>true</EnableDynamicLoading>`, reference PluginBase with `Private=false` + `ExcludeAssets=runtime`, implement `IIntegrationPlugin`.

### Existing Plugins

- **IntegrationPro.Plugin.PrismHR** — Extracts company data from PrismHR API. Uses `LoginService.createPeoSession` for auth, `ClientMasterService.getClientList` for data. Expects credentials with `PeoId` in `AdditionalFields` and optional `BaseUrl` in config.
- **IntegrationPro.Plugin.Mock** — Generates mock company data for testing. Supports config keys: `CompanyCount`, `DelayMs`, `SimulateFailure`.

### Service Bus Message Format

The `IntegrationRequestMessage` contains: `RequestId` (unique), `PluginName` (which plugin to load), `Credentials` (username, password, additional fields), `Configuration` (plugin-specific key/value pairs).

### Configuration

Configuration is in `appsettings.json` on the Worker project. Key sections:
- `ServiceBus:ConnectionString`, `ServiceBus:QueueName` — Azure Service Bus connection
- `Plugins:Directory` — Where published plugin DLLs live (default `/app/plugins` in container)
- `DataOutput:Directory` — Where extracted data is saved (default `/app/output` in container)

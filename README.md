# IntegrationPro

A containerized Azure App Job that performs ETL (Extract, Transform, Load) operations driven by Azure Service Bus messages. The application uses a plugin architecture to support multiple integration sources, with each plugin handling authentication, data extraction, transformation into a common model, and progress reporting via callbacks.

## Architecture

```
Azure Service Bus Queue
        |
        v
+-------------------+       +------------------------+
| ServiceBusConsumer | ----> | IntegrationOrchestrator |
| (BackgroundService)|       +------------------------+
+-------------------+              |
                                   |  1. Load plugin by name
                                   v
                          +---------------+
                          | PluginLoader  |
                          | (AssemblyLoad |
                          |    Context)   |
                          +---------------+
                                   |
                                   |  2. Initialize, Execute, Shutdown
                                   v
                        +--------------------+
                        | IIntegrationPlugin |
                        +--------------------+
                         /        |         \
                        v         v          v
                  OnProgress  OnDataReady  OnCompleted/OnFailed
                      |           |
                      v           v
              IProgressReporter  IDataSaver
              (logging)          (file system)
```

### Domain-Driven Design Layers

| Layer | Project | Responsibility |
|-------|---------|----------------|
| **Domain** | `IntegrationPro.Domain` | Entities (`IntegrationJob`), value objects, enums (`JobStatus`), Service Bus message contracts, common data models |
| **Application** | `IntegrationPro.Application` | Plugin loading via `AssemblyLoadContext`, ETL orchestration, `IDataSaver` and `IProgressReporter` abstractions |
| **Infrastructure** | `IntegrationPro.Infrastructure` | Azure Service Bus consumer, file system data saver, logging-based progress reporter, DI wiring |
| **Presentation** | `IntegrationPro.Worker` | .NET Worker Service entry point — hosts the Service Bus consumer |
| **Plugin Contracts** | `IntegrationPro.PluginBase` | Shared `IIntegrationPlugin` interface, `PluginContext`, and `PluginCredentials` — referenced by all plugins |

Dependency flow: `Worker` → `Infrastructure` → `Application` → `Domain` / `PluginBase`

Plugins reference only `PluginBase` and are loaded at runtime — they have no compile-time dependency on the host.

## Plugin System

Plugins follow the [.NET native plugin architecture](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support). Each plugin is loaded into its own `AssemblyLoadContext` with an `AssemblyDependencyResolver`, providing full dependency isolation between plugins and the host.

### Plugin Lifecycle

The `IntegrationOrchestrator` drives each plugin through three phases:

1. **`InitializeAsync(PluginContext)`** — Plugin receives credentials, configuration, a logger, and callback delegates. This is where authentication with the target system occurs (e.g., creating an API session).

2. **`ExecuteAsync(CancellationToken)`** — Plugin performs extraction and transformation. During execution, the plugin communicates with the host exclusively through callbacks on the `PluginContext`:

   | Callback | Signature | Purpose |
   |----------|-----------|---------|
   | `OnStarted` | `Func<string, Task>` | Signal that extraction has begun |
   | `OnProgress` | `Func<int, int, string, Task>` | Report step progress (currentStep, totalSteps, description) |
   | `OnDataReady` | `Func<string, Stream, Task>` | Deliver transformed data as a generic stream — the plugin does not dictate a storage model |
   | `OnFailed` | `Func<string, Exception?, Task>` | Report a failure with message and optional exception |
   | `OnCompleted` | `Func<string, Task>` | Signal successful completion with a summary |

3. **`ShutdownAsync()`** — Clean up resources (logout, dispose HTTP clients, close connections).

### Creating a New Plugin

1. Create a class library under `plugins/`:
   ```bash
   dotnet new classlib -o plugins/IntegrationPro.Plugin.MySource --framework net8.0
   dotnet sln add plugins/IntegrationPro.Plugin.MySource/IntegrationPro.Plugin.MySource.csproj
   ```

2. Configure the `.csproj` for dynamic loading:
   ```xml
   <PropertyGroup>
     <EnableDynamicLoading>true</EnableDynamicLoading>
   </PropertyGroup>

   <ItemGroup>
     <ProjectReference Include="..\..\src\IntegrationPro.PluginBase\IntegrationPro.PluginBase.csproj">
       <Private>false</Private>
       <ExcludeAssets>runtime</ExcludeAssets>
     </ProjectReference>
   </ItemGroup>
   ```
   `Private=false` and `ExcludeAssets=runtime` are required — without them the plugin's copy of `PluginBase.dll` conflicts with the host's copy and the interface type check fails at load time.

3. Implement `IIntegrationPlugin` in a public class. The `PluginLoader` discovers the implementation via reflection (`Activator.CreateInstance`), so the class must have a parameterless constructor.

4. Publish the plugin to the plugins directory:
   ```bash
   dotnet publish plugins/IntegrationPro.Plugin.MySource -c Release -o plugins-output/IntegrationPro.Plugin.MySource
   ```

The `PluginLoader` resolves plugins by convention: `{PluginsDirectory}/{PluginName}/{PluginName}.dll`. The `PluginName` comes from the Service Bus message.

## Included Plugins

### PrismHR (`IntegrationPro.Plugin.PrismHR`)

Extracts company/client data from the [PrismHR Services API](https://api-docs.prismhr.com).

**Authentication:** Creates a session via `LoginService.createPeoSession` using username, password, and PEO ID. The session token is sent as an `X-Session-Id` header on subsequent requests. On shutdown, the session is invalidated via `LoginService.invalidateSession`.

**Extraction:** Calls `ClientMasterService.getClientList` and transforms the response into a list of common company data objects.

**Data transformation mapping:**

| PrismHR Field | Common Model Field |
|---------------|-------------------|
| `clientId` | `ExternalId` |
| `clientName` | `CompanyName` |
| `legalName` | `LegalName` |
| `federalEin` | `FederalEin` |
| `status` | `Status` |
| `effectiveDate` | `EffectiveDate` |
| `address.*` | `PrimaryAddress` (Line1, Line2, City, State, PostalCode, Country) |
| `contact.*` | `PrimaryContact` (FirstName, LastName, Email, Phone, Title) |
| `sic`, `naics`, `stateOfIncorporation`, `payFrequency` | `ExtendedProperties` dictionary |

**Required credentials:**

| Field | Source | Description |
|-------|--------|-------------|
| Username | `Credentials.Username` | PrismHR web service user |
| Password | `Credentials.Password` | PrismHR web service password |
| PeoId | `Credentials.AdditionalFields["PeoId"]` | PEO identifier for session creation |

**Configuration:**

| Key | Default | Description |
|-----|---------|-------------|
| `BaseUrl` | `https://api.prismhr.com` | PrismHR API base URL |

### Mock (`IntegrationPro.Plugin.Mock`)

Generates synthetic company data for testing. Exercises all plugin callbacks including progress reporting and data delivery.

**Configuration:**

| Key | Default | Description |
|-----|---------|-------------|
| `CompanyCount` | `5` | Number of mock companies to generate |
| `DelayMs` | `100` | Millisecond delay between each company (simulates real extraction) |
| `SimulateFailure` | `false` | If `true`, throws an exception at 50% completion to test failure handling |

## Service Bus Message Format

Messages on the `integration-requests` queue must be JSON in this shape:

```json
{
  "requestId": "unique-request-id",
  "pluginName": "IntegrationPro.Plugin.Mock",
  "credentials": {
    "username": "user@example.com",
    "password": "password",
    "additionalFields": {
      "PeoId": "PEO-001"
    }
  },
  "configuration": {
    "CompanyCount": "10",
    "DelayMs": "50"
  }
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `requestId` | Yes | Unique identifier for correlation and output file organization |
| `pluginName` | Yes | Name of the plugin directory to load (e.g., `IntegrationPro.Plugin.PrismHR`) |
| `credentials` | Yes | Authentication data passed through to the plugin |
| `configuration` | No | Plugin-specific key/value pairs |

The `ServiceBusConsumer` deserializes the message, passes it to the `IntegrationOrchestrator`, and completes/abandons/dead-letters the message based on outcome.

## Configuration

Application settings are in `src/IntegrationPro.Worker/appsettings.json`:

```json
{
  "ServiceBus": {
    "ConnectionString": "",
    "QueueName": "integration-requests",
    "MaxConcurrentCalls": 1
  },
  "Plugins": {
    "Directory": "/app/plugins"
  },
  "DataOutput": {
    "Directory": "/app/output"
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `ServiceBus:ConnectionString` | (empty) | Azure Service Bus connection string |
| `ServiceBus:QueueName` | `integration-requests` | Queue to listen on |
| `ServiceBus:MaxConcurrentCalls` | `1` | How many messages to process concurrently |
| `Plugins:Directory` | `/app/plugins` | Directory where published plugin folders live |
| `DataOutput:Directory` | `/app/output` | Directory where extracted data is written |

In production, set the connection string via environment variables or Azure App Configuration:
```
ServiceBus__ConnectionString=Endpoint=sb://...
```

## Building and Running

### Build

```bash
dotnet build IntegrationPro.sln
```

### Publish Plugins

Each plugin must be published to its own subdirectory. The directory name must match the plugin assembly name:

```bash
dotnet publish plugins/IntegrationPro.Plugin.Mock -c Release -o plugins-output/IntegrationPro.Plugin.Mock
dotnet publish plugins/IntegrationPro.Plugin.PrismHR -c Release -o plugins-output/IntegrationPro.Plugin.PrismHR
```

### Run Locally

```bash
dotnet run --project src/IntegrationPro.Worker/IntegrationPro.Worker.csproj
```

Requires a valid `ServiceBus:ConnectionString` in configuration or environment variables.

### Test with Mock Plugin

A test harness is included to run plugins directly without Service Bus:

```bash
dotnet publish plugins/IntegrationPro.Plugin.Mock -c Release -o plugins-output/IntegrationPro.Plugin.Mock
dotnet run --project tests/IntegrationPro.TestHarness/IntegrationPro.TestHarness.csproj -- ./plugins-output
```

### Docker

```bash
docker build -t integrationpro .
docker run -e ServiceBus__ConnectionString="Endpoint=sb://..." integrationpro
```

The Dockerfile is a multi-stage build that:
1. Restores and publishes the Worker to `/app/publish`
2. Publishes each plugin to `/app/plugins/{PluginName}`
3. Runs on the `mcr.microsoft.com/dotnet/aspnet:8.0` runtime image

## Data Output

Extracted data is saved by the `FileSystemDataSaver` to:

```
{DataOutput:Directory}/{RequestId}/{dataType}_{yyyyMMdd_HHmmss}.json
```

For example: `/app/output/req-12345/companies_20260218_211500.json`

The `IDataSaver` interface is pluggable — replace `FileSystemDataSaver` with a blob storage or database implementation by registering a different implementation in `DependencyInjection.cs`.

## Project Structure

```
IntegrationPro/
├── IntegrationPro.sln
├── Dockerfile
├── src/
│   ├── IntegrationPro.PluginBase/        # Plugin contracts (IIntegrationPlugin, PluginContext)
│   ├── IntegrationPro.Domain/
│   │   ├── Entities/                     # IntegrationJob, JobProgressEntry
│   │   ├── Enums/                        # JobStatus
│   │   ├── Messages/                     # IntegrationRequestMessage, MessageCredentials
│   │   └── Models/                       # CommonCompanyData, CommonAddress, CommonContact
│   ├── IntegrationPro.Application/
│   │   ├── Interfaces/                   # IDataSaver, IProgressReporter
│   │   ├── PluginLoading/                # PluginLoader, PluginLoadContext
│   │   └── Services/                     # IntegrationOrchestrator
│   ├── IntegrationPro.Infrastructure/
│   │   ├── DataSaving/                   # FileSystemDataSaver
│   │   ├── Progress/                     # LoggingProgressReporter
│   │   ├── ServiceBus/                   # ServiceBusConsumer, ServiceBusOptions
│   │   └── DependencyInjection.cs        # Service registration
│   └── IntegrationPro.Worker/            # Entry point (Program.cs, appsettings.json)
├── plugins/
│   ├── IntegrationPro.Plugin.PrismHR/    # PrismHR API extraction plugin
│   └── IntegrationPro.Plugin.Mock/       # Mock data generation plugin
└── tests/
    └── IntegrationPro.TestHarness/       # Console app for testing plugins directly
```

## Synchronous API

`IntegrationPro.Api` is a second deployable in the same solution: an ASP.NET host that runs the same plugin pipeline synchronously over HTTP instead of consuming Service Bus messages. It exposes discovery endpoints so callers can enumerate installed plugins, fetch their versions, and pull the JSON Schemas for credentials and configuration — then POST a request and get the extracted data back in a single round trip. A schema-driven React playground UI is bundled at `/ui/` for interactive testing.

### Run Locally

```bash
dotnet run --project src/IntegrationPro.Api/IntegrationPro.Api.csproj
```

The Api listens on port `8081` by default. Swagger lives at `/swagger`, the playground UI at `/ui/`.

### Endpoints

| Method | Route | Purpose |
|--------|-------|---------|
| `GET` | `/plugins` | List installed plugins |
| `GET` | `/plugins/{name}/versions` | List available versions for a plugin |
| `GET` | `/plugins/{name}/{version}/schema` | Get the JSON Schema for credentials + configuration |
| `POST` | `/integrations/run` | Execute a plugin synchronously and return the emitted data |

### Docker

The Api has its own Dockerfile at `src/IntegrationPro.Api/Dockerfile`. It is a multi-stage build: Node stage compiles the React playground, the .NET SDK stage publishes the Api (with `SkipUiBuild=true` so MSBuild doesn't re-run npm) and both bundled plugins, and the aspnet runtime stage assembles the final image.

```bash
docker build -t integrationpro-api -f src/IntegrationPro.Api/Dockerfile .
```

Run this from the repository root so the multi-stage `COPY` instructions can see `src/`, `plugins/`, and `IntegrationPro.sln`.

### Two Deployables, One Solution

- `IntegrationPro.Worker` (port `8080`) keeps running Service Bus-driven asynchronous ETL.
- `IntegrationPro.Api` (port `8081`) serves synchronous HTTP execution and the discovery/playground surface.

Both share `IntegrationPro.Application` + `IntegrationPro.Infrastructure`, so plugins, catalog, and orchestration behave identically. They scale independently.

See the design doc at [`docs/plans/2026-04-21-sync-integration-api-design.md`](docs/plans/2026-04-21-sync-integration-api-design.md) for more detail.

# Synchronous Integration API — Design

**Date:** 2026-04-21
**Status:** Design approved, ready for implementation planning
**Owner:** corey.fournier@tapcheck.com

## Problem

IntegrationPro today executes ETL plugins asynchronously via Azure Service Bus. Callers enqueue a message, a Worker picks it up, and results are written via `IDataSaver`. There is no way to invoke a plugin and receive results in a single HTTP round-trip, and no machine-readable catalog of what plugins exist or what options they accept.

We need:

1. A **synchronous HTTP API** that runs a plugin and returns its output in the response body.
2. A **discoverable catalog** so callers can enumerate plugins, versions, and per-plugin option schemas via OpenAPI / JSON Schema.
3. A **playground UI** that uses the discovery endpoints to generate forms dynamically for exercising any plugin.

## Constraints

- Plugins are planned to ship as **NuGet packages from a separate repository**. Eventually hundreds of plugins × multiple versions each. The API host cannot have compile-time references to individual plugins.
- The existing Service Bus path must keep working unchanged — it's the right tool for long-running, multi-emission, or fire-and-forget extractions.
- Plugin authors should keep writing idiomatic C# — no hand-rolled JSON Schema files.

## Non-goals for v1

- Authentication / authorization (stub with middleware hook; add later).
- Streaming progress in the sync response body (use Service Bus + a future `GET /jobs/{id}` endpoint for that).
- Multi-emission plugins over the sync API (they stay on Service Bus).
- Hot-reload of plugins without restart.
- Persistent run history server-side (playground UI keeps last-10 in `localStorage`).

---

## Architecture Overview

```
┌─────────────────────────────┐        ┌──────────────────────────────┐
│  IntegrationPro.Worker      │        │  IntegrationPro.Api (NEW)    │
│  (Service Bus only)         │        │  - HTTP sync API             │
│  - ServiceBusConsumer       │        │  - Swagger / OpenAPI         │
│  - /healthz/live,ready      │        │  - Playground UI (wwwroot)   │
└──────────────┬──────────────┘        └──────────────┬───────────────┘
               │                                       │
               └───────────┬───────────────────────────┘
                           ▼
             ┌──────────────────────────────┐
             │   IntegrationPro.Application │
             │   - IntegrationOrchestrator  │
             │   - IPluginCatalog (NEW)     │
             │   - PluginLoader             │
             └──────────────┬───────────────┘
                            ▼
             ┌──────────────────────────────┐
             │   IntegrationPro.Domain      │
             │   IntegrationPro.PluginBase  │  ← Plugin authors depend on this
             └──────────────────────────────┘
```

### New project: `IntegrationPro.Api`

ASP.NET Core WebApplication, sibling to `Worker`. References `Application` and `Infrastructure`. Hosts:

- Sync execution endpoint + discovery endpoints.
- Swashbuckle-generated Swagger JSON + Swagger UI.
- `wwwroot/ui/` serving the built React playground.

### New abstraction: `IPluginCatalog`

Lives in `Application`. Abstracts "what plugins exist, what versions, what schemas."

- **Today's implementation:** `DiskReflectionPluginCatalog` in `Infrastructure`. Scans `/app/plugins/{Name}/{Version}/` at startup, loads each plugin into its own ALC, reflects on its `ConfigType` / `CredentialsType`, generates JSON Schema via NJsonSchema, caches in memory.
- **Tomorrow's implementation:** `NuGetFeedPluginCatalog` (out of scope for v1). Lazy pull from a NuGet feed: a cache miss on resolve triggers download + unpack + load. No controller or UI code changes — the interface absorbs the swap.

Registration in DI includes `// TODO: replace with NuGetFeedPluginCatalog when feed-backed resolution is ready.`

### Worker unchanged

Worker continues consuming Service Bus messages against the same shared `Application` + `Infrastructure`. Both hosts read plugins from the same configured `Plugins:Directory`. In a container world this is a shared volume or baked into the image.

### Deployment

Two deployable units from one solution — unchanged `Worker` image and a new `Api` image. They scale independently (API is request-driven bursty; Worker is steady queue-drain).

---

## Plugin Contract Changes

`IIntegrationPlugin` grows three properties so the reflection-based catalog can generate schemas without a second load pass:

```csharp
public interface IIntegrationPlugin
{
    string Name { get; }
    string Description { get; }
    string Version { get; }          // NEW — e.g., "1.2.0", used by catalog for version listing

    Type ConfigType { get; }         // NEW — POCO describing plugin configuration
    Type CredentialsType { get; }    // NEW — POCO describing credentials

    Task InitializeAsync(PluginContext context);
    Task ExecuteAsync(CancellationToken ct);
    Task ShutdownAsync();
}
```

### Plugin authors write POCOs with DataAnnotations

```csharp
public sealed class PrismHrConfig
{
    [Required, Description("Base URL of the PrismHR API endpoint.")]
    public string BaseUrl { get; init; } = "";

    [Description("Page size for client list retrieval.")]
    [Range(1, 500)]
    public int PageSize { get; init; } = 100;
}

public sealed class PrismHrCredentials
{
    [Required] public string Username { get; init; } = "";
    [Required] public string Password { get; init; } = "";
    [Required, Description("PrismHR PEO identifier.")]
    public string PeoId { get; init; } = "";
}
```

### Schema generation

At startup, `DiskReflectionPluginCatalog` uses `NJsonSchema.Generation.JsonSchemaGenerator` against each plugin's `ConfigType` and `CredentialsType`. The generator honors `[Required]`, `[Range]`, `[Description]`, `[DefaultValue]`, enums, and nested objects. Schemas are cached in the catalog and served verbatim from `GET /plugins/{name}/{version}/schema`.

### Runtime binding

The sync API deserializes inbound JSON for `credentials` and `configuration` into the plugin's POCO types using `System.Text.Json` (resolving types against the plugin's ALC). The host then flattens the typed objects into the dictionary-shaped `PluginContext.Configuration` + `PluginCredentials` that the orchestrator already consumes. **The orchestrator itself is not modified.**

### Service Bus path unchanged

`IntegrationRequestMessage` stays dictionary-based; Worker keeps working without changes. Plugin authors who don't add the new properties get compile errors — Mock and PrismHR are migrated as part of this change.

---

## Discovery Layer

### Directory layout

```
/app/plugins/
  PrismHR/
    1.0.0/ { IntegrationPro.Plugin.PrismHR.dll, ...deps }
    1.1.0/ { ... }
  Mock/
    1.0.0/ { ... }
```

The version segment is new; today's layout has only `{Name}/{Name}.dll`. Worker and Api both switch to the versioned layout.

### Interface

```csharp
public interface IPluginCatalog
{
    IReadOnlyList<PluginSummary> ListPlugins();
    IReadOnlyList<string> ListVersions(string pluginName);
    PluginSchema GetSchema(string pluginName, string version);
    IIntegrationPlugin Resolve(string pluginName, string? version);  // null = latest
}

public record PluginSummary(string Name, string LatestVersion, string Description);

public record PluginSchema(
    string Name,
    string Version,
    string Description,
    JsonSchema Config,
    JsonSchema Credentials);
```

### `DiskReflectionPluginCatalog` startup sequence

1. Enumerate `{Name}/{Version}/` folders.
2. Load each plugin once into its own `PluginLoadContext`, read `ConfigType` / `CredentialsType`.
3. Generate JSON Schema via NJsonSchema; cache.
4. Keep loaded plugin instances warm (N is small today; revisit when moving to feed-backed catalog).

### Endpoints

| Endpoint | Response |
|---|---|
| `GET /plugins?page=1&pageSize=50&search=` | `{ items: [{name, latestVersion, description}], total, page, pageSize }` |
| `GET /plugins/{name}/versions` | `["1.1.0", "1.0.0"]` — semver-sorted, latest first |
| `GET /plugins/{name}/{version}/schema` | `{ name, version, description, config: <JsonSchema>, credentials: <JsonSchema> }` |

### Versioning

Semver parsing via `System.Version` for MVP. Latest = highest semver. Callers may omit `version` on the execution endpoint to get the latest at the moment of the call.

---

## Execution Pipeline

### Endpoint

`POST /integrations/run`

```json
{
  "pluginName": "PrismHR",
  "version": "1.1.0",
  "timeoutSeconds": 120,
  "credentials": { "username": "...", "password": "...", "peoId": "..." },
  "configuration": { "baseUrl": "https://...", "pageSize": 100 }
}
```

`version` and `timeoutSeconds` are optional. Omitted version → latest. Omitted timeout → server hard cap.

### Flow

1. **Resolve & validate.** `IPluginCatalog.GetSchema` → NJsonSchema validator over inbound `credentials` + `configuration`. Invalid → `400` with a structured error list; no plugin loaded.
2. **Build `IntegrationRequestMessage`.** Generate `RequestId` (GUID). Flatten typed config/credentials into the existing dictionary shape. Orchestrator code is unchanged.
3. **Compose cancellation.** Link three sources: `HttpContext.RequestAborted`, server hard cap (`Api:MaxRequestSeconds`, default 300), client `timeoutSeconds` (clamped to hard cap).
4. **Sync-mode callbacks.** The orchestrator receives an `IDataSaver` that writes into a `FileBufferingWriteStream` (Microsoft.AspNetCore.WebUtilities) — 30 KB in memory, spills to temp file beyond that. A `SingleEmissionGuard` wrapper throws if `OnDataReady` is invoked more than once.
5. **Success path.** Plugin completes → set `Content-Type` (from plugin `[OutputContentType]` attribute; defaults to `application/json`) → `await buffer.DrainBufferAsync(response.Body)` → `200`. Add `X-Request-Id` and `X-Plugin-Version` response headers.
6. **Error path.** Exception → discard buffer → respond with structured JSON:

```json
{
  "requestId": "...",
  "pluginName": "PrismHR",
  "version": "1.1.0",
  "status": "Failed",
  "error": { "code": "...", "message": "...", "details": [ ... ] }
}
```

### HTTP status mapping

| Condition | Status |
|---|---|
| JSON Schema validation failed | `400` |
| Plugin or version not found | `404` |
| Timeout (hard cap or client timeout hit) | `408` |
| Multi-emission attempted in sync mode | `409` |
| Plugin execution failure | `500` |

### Code comment near the buffer

```csharp
// Buffered to allow proper 4xx/5xx error responses on mid-run failures.
// Optimization: swap for direct pipe-through (plugin stream -> response.Body)
// if streaming semantics and lower TTFB become more important than clean
// error status codes.
```

---

## Playground UI

### Project

`src/IntegrationPro.Api.Ui/` — Vite + React + TypeScript, dependencies: `@rjsf/core`, `@rjsf/validator-ajv8`, `@rjsf/utils`.

### Build integration

`IntegrationPro.Api.csproj` adds an MSBuild target that:

1. Detects whether `package-lock.json` / sources changed since last build.
2. If dirty, runs `npm ci && npm run build`.
3. Copies `dist/*` into `Api/wwwroot/ui/`.

A single `dotnet build` builds both. No separate CI stage for the UI.

### Routes (all served by Api)

- `/` → redirects to `/ui/`.
- `/ui/` → React playground.
- `/swagger` → Swagger UI.
- `/swagger/v1/swagger.json` → OpenAPI document generated by Swashbuckle. It documents only the **four generic operations** (3 discovery + 1 execution) — not per-plugin endpoints. Per-plugin schemas are discoverable via `/plugins/{name}/{version}/schema`.

### Flow

1. Load `GET /plugins?pageSize=50` with search + paging for the 300-plugin case.
2. Select plugin → `GET /plugins/{name}/versions` → version dropdown (defaults to latest).
3. On version selection → `GET /plugins/{name}/{version}/schema` → render **two RJSF forms** (credentials / configuration) hydrated from the returned JSON Schemas.
4. "Run" button → `POST /integrations/run`. Pending state shows a disabled button + spinner; Cancel aborts the fetch, which propagates to `HttpContext.RequestAborted` server-side.
5. Response rendering by `Content-Type`:
   - `application/json` → pretty-printed collapsible tree.
   - `text/csv` → preview first 100 rows in a table, "Download full" link.
   - Other → "Download" button with raw blob.
   - Error (`status !== 2xx`) → red banner with `error.code`, `message`, and the structured `details` list.
6. Last 10 runs kept in `localStorage` for quick re-run (cred + config per browser). UI warns that secrets are stored locally.

### Out of scope for v1

- Auth UI.
- Server-side named presets.
- Streaming progress.

---

## Cross-cutting

### Auth

Stub middleware hook. No enforcement in v1. Documented as an assumption — Api is deployed on an internal network. `// TODO: plug in API-key or OAuth middleware here` in the Api's `Program.cs` pipeline.

### Configuration additions

Added to the Api's `appsettings.json`:

```json
{
  "Plugins": { "Directory": "/app/plugins" },
  "Api": {
    "MaxRequestSeconds": 300,
    "BufferThresholdBytes": 30720
  }
}
```

### Logging & traceability

Every execution logs with `RequestId`, `PluginName`, `Version`. Response headers `X-Request-Id` and `X-Plugin-Version` echo these for client-side correlation.

### Health checks

Api exposes the same `/healthz/live` and `/healthz/ready` style endpoints as Worker. Readiness goes unhealthy if the catalog failed to load any plugins at startup.

---

## Migration & rollout

1. Add new properties to `IIntegrationPlugin`. Migrate `Mock` and `PrismHR` plugins: add `Version`, `ConfigType`, `CredentialsType`, and their POCO types.
2. Switch plugins directory layout to `{Name}/{Version}/`. Update `PluginLoader` to accept a version segment; update Worker's plugin publish scripts.
3. Introduce `IPluginCatalog` and `DiskReflectionPluginCatalog`.
4. Create `IntegrationPro.Api` project. Add discovery + execution endpoints, Swagger, health checks.
5. Create `IntegrationPro.Api.Ui` project with Vite scaffold; wire MSBuild target.
6. End-to-end smoke test against Mock plugin via both Swagger UI and the playground.
7. End-to-end test against PrismHR plugin.
8. Document the Api image + deployment in `README.md`.

## Open items for future iterations

- `NuGetFeedPluginCatalog` for lazy pull at scale (300+ plugins).
- Auth (API key or OAuth).
- `GET /jobs/{requestId}` read-side for Service-Bus-initiated jobs.
- Direct pipe-through (skip the buffer) when TTFB matters more than clean error status codes.
- Hot-reload of plugins without restart.
- Streaming progress (HTTP/2 trailers or SSE companion endpoint).

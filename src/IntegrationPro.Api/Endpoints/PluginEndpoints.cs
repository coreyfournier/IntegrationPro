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

        app.MapGet("/plugins/{name}/{version}/schema", async (string name, string version, IPluginCatalog catalog, CancellationToken ct) =>
        {
            try
            {
                var schema = await catalog.GetSchemaAsync(name, version, ct);
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

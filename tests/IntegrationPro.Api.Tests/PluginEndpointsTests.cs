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

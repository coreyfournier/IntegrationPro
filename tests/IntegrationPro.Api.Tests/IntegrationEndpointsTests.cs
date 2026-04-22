using System.Net;
using System.Net.Http.Json;
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
        resp.Headers.Should().Contain(h => string.Equals(h.Key, "X-Request-Id", StringComparison.OrdinalIgnoreCase));
        resp.Headers.Should().Contain(h => string.Equals(h.Key, "X-Plugin-Version", StringComparison.OrdinalIgnoreCase));

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
            Credentials: new JsonObject(),
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
            Configuration: new JsonObject { ["companyCount"] = 4, ["failureMode"] = "Halfway", ["delayMs"] = 0 });

        var resp = await client.PostAsJsonAsync("/integrations/run", req);
        resp.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Status.Should().Be("Failed");
        err.Error.Message.Should().Contain("Halfway");
        err.Error.Category.Should().Be("unexpected");
        err.Error.ExceptionType.Should().Be("InvalidOperationException");
        err.Error.Retryable.Should().BeNull();
    }
}

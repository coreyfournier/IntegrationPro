using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using FluentAssertions;
using IntegrationPro.Api.Execution;
using Xunit;

namespace IntegrationPro.Api.Tests;

public sealed class ExceptionClassifierTests
{
    [Fact]
    public void UriFormat_is_configuration_non_retryable()
    {
        var d = ExceptionClassifier.Classify(new UriFormatException("bad uri"));
        d.Category.Should().Be("configuration");
        d.Retryable.Should().Be(false);
        d.ExceptionType.Should().Be(nameof(UriFormatException));
        d.UpstreamStatus.Should().BeNull();
    }

    [Fact]
    public void HttpRequest_with_5xx_is_upstream_http_retryable()
    {
        var d = ExceptionClassifier.Classify(new HttpRequestException("boom", null, HttpStatusCode.BadGateway));
        d.Category.Should().Be("upstream_http");
        d.Retryable.Should().Be(true);
        d.UpstreamStatus.Should().Be(502);
    }

    [Fact]
    public void HttpRequest_with_4xx_is_upstream_http_non_retryable()
    {
        var d = ExceptionClassifier.Classify(new HttpRequestException("nope", null, HttpStatusCode.Forbidden));
        d.Category.Should().Be("upstream_http");
        d.Retryable.Should().Be(false);
        d.UpstreamStatus.Should().Be(403);
    }

    [Fact]
    public void HttpRequest_with_null_status_is_retryable_network_level()
    {
        var d = ExceptionClassifier.Classify(new HttpRequestException("DNS failure"));
        d.Category.Should().Be("upstream_http");
        d.Retryable.Should().Be(true);
        d.UpstreamStatus.Should().BeNull();
    }

    [Fact]
    public void TaskCanceled_is_upstream_timeout_retryable()
    {
        var d = ExceptionClassifier.Classify(new TaskCanceledException("timed out"));
        d.Category.Should().Be("upstream_timeout");
        d.Retryable.Should().Be(true);
    }

    [Fact]
    public void AuthenticationException_is_authentication_non_retryable()
    {
        var d = ExceptionClassifier.Classify(new AuthenticationException("bad cert"));
        d.Category.Should().Be("authentication");
        d.Retryable.Should().Be(false);
    }

    [Fact]
    public void Unauthorized_is_authentication_non_retryable()
    {
        var d = ExceptionClassifier.Classify(new UnauthorizedAccessException("denied"));
        d.Category.Should().Be("authentication");
        d.Retryable.Should().Be(false);
    }

    [Fact]
    public void Unknown_exception_is_unexpected_unknown_retryability()
    {
        var d = ExceptionClassifier.Classify(new InvalidOperationException("weird state"));
        d.Category.Should().Be("unexpected");
        d.Retryable.Should().BeNull();
        d.ExceptionType.Should().Be(nameof(InvalidOperationException));
    }
}

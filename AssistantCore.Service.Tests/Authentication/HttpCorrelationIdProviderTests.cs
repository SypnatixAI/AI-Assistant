using AssistantCore.Service.Infrastructure.Authentication;
using Microsoft.AspNetCore.Http;

namespace AssistantCore.Service.Tests.Authentication;

public sealed class HttpCorrelationIdProviderTests
{
    [Fact]
    public void Given_AnActiveHttpRequest_When_GetCorrelationId_Then_ReturnsTheTraceIdentifier()
    {
        // Given
        var context = new DefaultHttpContext { TraceIdentifier = "request-8f812" };
        var provider = new HttpCorrelationIdProvider(new HttpContextAccessor { HttpContext = context });

        // When
        var correlationId = provider.GetCorrelationId();

        // Then
        Assert.Equal("request-8f812", correlationId);
    }

    [Fact]
    public void Given_NoActiveHttpRequest_When_GetCorrelationId_Then_Throws()
    {
        // Given
        var provider = new HttpCorrelationIdProvider(new HttpContextAccessor());

        // When / Then
        Assert.Throws<InvalidOperationException>(provider.GetCorrelationId);
    }
}

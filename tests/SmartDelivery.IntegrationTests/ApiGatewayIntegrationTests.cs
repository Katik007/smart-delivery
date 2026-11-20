using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SmartDelivery.IntegrationTests;

public class ApiGatewayIntegrationTests : IClassFixture<WebApplicationFactory<SmartDelivery.ApiGateway.Program>>
{
    private readonly HttpClient _client;

    public ApiGatewayIntegrationTests(WebApplicationFactory<SmartDelivery.ApiGateway.Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GatewayHealth_ReturnsHealthyStatusAndStudentInfo()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
        content.Should().Contain("Кабанова Екатерина");
        content.Should().Contain("kabanovae.9593647-gu32875.twc1.net");
    }

    [Fact]
    public async Task GatewayRoot_ReturnsIndexHtmlDashboard()
    {
        var response = await _client.GetAsync("/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("SmartDelivery");
        html.Should().Contain("Кабанова Екатерина");
    }
}

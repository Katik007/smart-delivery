using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SmartDelivery.CatalogService.Application.DTOs;
using Xunit;

namespace SmartDelivery.IntegrationTests;

public class CatalogIntegrationTests : IClassFixture<CustomWebApplicationFactory<SmartDelivery.CatalogService.Program>>
{
    private readonly HttpClient _client;

    public CatalogIntegrationTests(CustomWebApplicationFactory<SmartDelivery.CatalogService.Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthyStatus()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task GetCategories_ReturnsSuccessAndCategoriesList()
    {
        var response = await _client.GetAsync("/api/catalog/categories");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();
        categories.Should().NotBeNull();
    }

    [Fact]
    public async Task GetProducts_ReturnsSeededProducts()
    {
        var response = await _client.GetAsync("/api/catalog/products");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();
        products.Should().NotBeNull();
    }
}

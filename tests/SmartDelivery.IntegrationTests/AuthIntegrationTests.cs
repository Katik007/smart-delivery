using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SmartDelivery.AuthService.Application.DTOs;
using Xunit;

namespace SmartDelivery.IntegrationTests;

public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory<SmartDelivery.AuthService.Program>>
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(CustomWebApplicationFactory<SmartDelivery.AuthService.Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Healthy");
    }

    [Fact]
    public async Task RegisterAndLogin_ReturnsValidAuthTokens()
    {
        // Arrange
        var username = $"user_{Guid.NewGuid():N}";
        var registerRequest = new RegisterRequest(
            username,
            $"{username}@delivery.test",
            "SuperPassword123!",
            "Customer"
        );

        // Act 1: Register
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var regResult = await regResponse.Content.ReadFromJsonAsync<AuthResponse>();
        regResult.Should().NotBeNull();
        regResult!.AccessToken.Should().NotBeNullOrWhiteSpace();

        // Act 2: Login
        var loginRequest = new LoginRequest(registerRequest.Email, registerRequest.Password);
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginError = await loginResponse.Content.ReadAsStringAsync();
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: loginError);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        loginResult.Should().NotBeNull();
        loginResult!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }
}

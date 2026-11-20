using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using SmartDelivery.Shared.Security;
using Xunit;

namespace SmartDelivery.UnitTests.Security;

public class JwtTokenHelperTests
{
    private readonly JwtSettings _settings = new()
    {
        Secret = "SuperSecureKeyWithEnoughLengthForHmacSha256Signature2026!",
        Issuer = "SmartDelivery.Auth",
        Audience = "SmartDelivery.Clients",
        ExpiryMinutes = 30
    };

    [Fact]
    public void GenerateToken_GeneratesValidJwtWithCorrectClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var username = "ekaterina";
        var email = "ekaterina@domain.com";
        var role = Roles.Admin;

        // Act
        var tokenString = JwtTokenHelper.GenerateToken(userId, username, email, role, _settings);

        // Assert
        tokenString.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(tokenString).Should().BeTrue();

        var jwt = handler.ReadJwtToken(tokenString);
        jwt.Issuer.Should().Be(_settings.Issuer);
        jwt.Audiences.Should().Contain(_settings.Audience);

        var nameClaim = jwt.Claims.FirstOrDefault(c => c.Type == "unique_name" || c.Type == ClaimTypes.Name);
        nameClaim?.Value.Should().Be(username);

        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role);
        roleClaim?.Value.Should().Be(Roles.Admin);
    }
}

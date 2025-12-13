using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using NuxtIdentity.Core.Configuration;
using NuxtIdentity.Core.Services;
using NuxtIdentity.Core.Tests.Helpers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NuxtIdentity.Core.Tests.Services;

[TestFixture]
[Category("Unit")]
public class JwtTokenServiceTests
{
    private Mock<ILogger<JwtTokenService<TestUser>>> _loggerMock = null!;
    private JwtOptions _jwtOptions = null!;
    private TestUserClaimsProvider _claimsProvider = null!;
    private JwtTokenService<TestUser> _service = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<JwtTokenService<TestUser>>>();
        _jwtOptions = TestJwtOptions.CreateDefault();
        _claimsProvider = new TestUserClaimsProvider();

        var optionsMock = new Mock<IOptions<JwtOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_jwtOptions);

        _service = new JwtTokenService<TestUser>(
            optionsMock.Object,
            new[] { _claimsProvider },
            _loggerMock.Object
        );
    }

    [Test]
    public async Task GenerateAccessTokenAsync_ValidUser_ReturnsValidJwtToken()
    {
        // Arrange
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };

        // Act
        var token = await _service.GenerateAccessTokenAsync(user);

        // Assert
        token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();
    }

    [Test]
    public async Task GenerateAccessTokenAsync_ValidUser_TokenContainsUserClaims()
    {
        // Arrange
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };

        // Act
        var token = await _service.GenerateAccessTokenAsync(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == user.Username);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == user.Email);
    }

    [Test]
    public async Task GenerateAccessTokenAsync_ValidUser_TokenContainsStandardClaims()
    {
        // Arrange
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };

        // Act
        var token = await _service.GenerateAccessTokenAsync(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Claims.Should().Contain(c => c.Type == "iat"); // Issued at
        jwtToken.Claims.Should().Contain(c => c.Type == "nbf"); // Not before
        jwtToken.Issuer.Should().Be(_jwtOptions.Issuer);
        jwtToken.Audiences.Should().Contain(_jwtOptions.Audience);
    }

    [Test]
    public async Task GenerateAccessTokenAsync_ValidUser_TokenExpiresAtCorrectTime()
    {
        // Arrange
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };
        var beforeGeneration = DateTime.UtcNow;

        // Act
        var token = await _service.GenerateAccessTokenAsync(user);

        // Assert
        var afterGeneration = DateTime.UtcNow;
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var expectedExpiration = beforeGeneration.Add(_jwtOptions.Lifespan);
        jwtToken.ValidTo.Should().BeCloseTo(expectedExpiration, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task ValidateTokenAsync_ValidToken_ReturnsClaimsPrincipal()
    {
        // Arrange
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };
        var token = await _service.GenerateAccessTokenAsync(user);

        // Act
        var principal = await _service.ValidateTokenAsync(token);

        // Assert
        principal.Should().NotBeNull();
        principal!.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id);
        principal.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == user.Username);
    }

    [Test]
    public async Task ValidateTokenAsync_ValidToken_PrincipalContainsAllClaims()
    {
        // Arrange
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };
        var token = await _service.GenerateAccessTokenAsync(user);

        // Act
        var principal = await _service.ValidateTokenAsync(token);

        // Assert
        principal.Should().NotBeNull();
        var claims = principal!.Claims.ToList();

        claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "user123");
        claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "testuser");
        claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == "test@example.com");
    }

    [Test]
    public void GetTokenValidationParameters_ReturnsCorrectConfiguration()
    {
        // Act
        var parameters = _service.GetTokenValidationParameters();

        // Assert
        parameters.Should().NotBeNull();
        parameters.ValidateIssuerSigningKey.Should().BeTrue();
        parameters.ValidateIssuer.Should().BeTrue();
        parameters.ValidIssuer.Should().Be(_jwtOptions.Issuer);
        parameters.ValidateAudience.Should().BeTrue();
        parameters.ValidAudience.Should().Be(_jwtOptions.Audience);
        parameters.ValidateLifetime.Should().BeTrue();
        parameters.ClockSkew.Should().Be(TimeSpan.Zero);
    }

    [Test]
    public async Task GenerateAccessTokenAsync_MultipleUsers_GeneratesUniqueTokens()
    {
        // Arrange
        var user1 = new TestUser { Id = "user1", Username = "user1", Email = "user1@example.com" };
        var user2 = new TestUser { Id = "user2", Username = "user2", Email = "user2@example.com" };

        // Act
        var token1 = await _service.GenerateAccessTokenAsync(user1);
        var token2 = await _service.GenerateAccessTokenAsync(user2);

        // Assert
        token1.Should().NotBe(token2);

        var handler = new JwtSecurityTokenHandler();
        var jwt1 = handler.ReadJwtToken(token1);
        var jwt2 = handler.ReadJwtToken(token2);

        jwt1.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value.Should().Be("user1");
        jwt2.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value.Should().Be("user2");
    }
}

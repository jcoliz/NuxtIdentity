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
        // Given a valid test user
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };

        // When generating an access token
        var token = await _service.GenerateAccessTokenAsync(user);

        // Then the token should not be empty
        token.Should().NotBeNullOrEmpty();

        // And it should be a valid JWT token
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();
    }

    [Test]
    public async Task GenerateAccessTokenAsync_ValidUser_TokenContainsUserClaims()
    {
        // Given a valid test user
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };

        // When generating an access token
        var token = await _service.GenerateAccessTokenAsync(user);

        // Then the token should contain user claims
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // And it should have the user ID claim
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id);
        // And the username claim
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == user.Username);
        // And the email claim
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == user.Email);
    }

    [Test]
    public async Task GenerateAccessTokenAsync_ValidUser_TokenContainsStandardClaims()
    {
        // Given a valid test user
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };

        // When generating an access token
        var token = await _service.GenerateAccessTokenAsync(user);

        // Then the token should contain standard JWT claims
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // And it should have the issued-at claim
        jwtToken.Claims.Should().Contain(c => c.Type == "iat");
        // And the not-before claim
        jwtToken.Claims.Should().Contain(c => c.Type == "nbf");
        // And the correct issuer
        jwtToken.Issuer.Should().Be(_jwtOptions.Issuer);
        // And the correct audience
        jwtToken.Audiences.Should().Contain(_jwtOptions.Audience);
    }

    [Test]
    public async Task GenerateAccessTokenAsync_ValidUser_TokenExpiresAtCorrectTime()
    {
        // Given a valid test user
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };
        // And the current time before generation
        var beforeGeneration = DateTime.UtcNow;

        // When generating an access token
        var token = await _service.GenerateAccessTokenAsync(user);

        // Then the token should expire at the configured lifespan
        var afterGeneration = DateTime.UtcNow;
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var expectedExpiration = beforeGeneration.Add(_jwtOptions.Lifespan);
        jwtToken.ValidTo.Should().BeCloseTo(expectedExpiration, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task ValidateTokenAsync_ValidToken_ReturnsClaimsPrincipal()
    {
        // Given a valid test user
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };
        // And a generated access token for that user
        var token = await _service.GenerateAccessTokenAsync(user);

        // When validating the token
        var principal = await _service.ValidateTokenAsync(token);

        // Then the principal should not be null
        principal.Should().NotBeNull();
        // And it should contain the user ID claim
        principal!.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id);
        // And the username claim
        principal.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == user.Username);
    }

    [Test]
    public async Task ValidateTokenAsync_ValidToken_PrincipalContainsAllClaims()
    {
        // Given a valid test user
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };
        // And a generated access token for that user
        var token = await _service.GenerateAccessTokenAsync(user);

        // When validating the token
        var principal = await _service.ValidateTokenAsync(token);

        // Then the principal should not be null
        principal.Should().NotBeNull();
        var claims = principal!.Claims.ToList();

        // And it should contain all the user claims
        claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "user123");
        claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "testuser");
        claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == "test@example.com");
    }

    [Test]
    public void GetTokenValidationParameters_ReturnsCorrectConfiguration()
    {
        // When getting token validation parameters
        var parameters = _service.GetTokenValidationParameters();

        // Then the parameters should not be null
        parameters.Should().NotBeNull();
        // And issuer signing key validation should be enabled
        parameters.ValidateIssuerSigningKey.Should().BeTrue();
        // And issuer validation should be enabled
        parameters.ValidateIssuer.Should().BeTrue();
        parameters.ValidIssuer.Should().Be(_jwtOptions.Issuer);
        // And audience validation should be enabled
        parameters.ValidateAudience.Should().BeTrue();
        parameters.ValidAudience.Should().Be(_jwtOptions.Audience);
        // And lifetime validation should be enabled
        parameters.ValidateLifetime.Should().BeTrue();
        // And clock skew should be zero
        parameters.ClockSkew.Should().Be(TimeSpan.Zero);
    }

    [Test]
    public async Task GenerateAccessTokenAsync_MultipleUsers_GeneratesUniqueTokens()
    {
        // Given two different users
        var user1 = new TestUser { Id = "user1", Username = "user1", Email = "user1@example.com" };
        var user2 = new TestUser { Id = "user2", Username = "user2", Email = "user2@example.com" };

        // When generating tokens for both users
        var token1 = await _service.GenerateAccessTokenAsync(user1);
        var token2 = await _service.GenerateAccessTokenAsync(user2);

        // Then the tokens should be different
        token1.Should().NotBe(token2);

        // And each token should contain the correct user ID
        var handler = new JwtSecurityTokenHandler();
        var jwt1 = handler.ReadJwtToken(token1);
        var jwt2 = handler.ReadJwtToken(token2);

        jwt1.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value.Should().Be("user1");
        jwt2.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value.Should().Be("user2");
    }
}

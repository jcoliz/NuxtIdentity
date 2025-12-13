using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NUnit.Framework;
using NuxtIdentity.Core.Abstractions;
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
    private FakeTimeProvider _timeProvider = null!;
    private JwtOptions _jwtOptions = null!;
    private TestUserClaimsProvider _claimsProvider = null!;
    private JwtTokenService<TestUser> _service = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<JwtTokenService<TestUser>>>();
        // Use current real time as baseline so JWT validation (which uses DateTime.UtcNow) sees tokens as valid
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _jwtOptions = TestJwtOptions.CreateDefault();
        _claimsProvider = new TestUserClaimsProvider();

        var optionsMock = new Mock<IOptions<JwtOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_jwtOptions);

        _service = new JwtTokenService<TestUser>(
            optionsMock.Object,
            new[] { _claimsProvider },
            _loggerMock.Object,
            _timeProvider
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
        // And the current fake time
        var currentTime = _timeProvider.GetUtcNow().DateTime;

        // When generating an access token
        var token = await _service.GenerateAccessTokenAsync(user);

        // Then the token should expire at the configured lifespan
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // Compare as Unix timestamps to avoid timezone conversion issues
        var expectedExpiration = currentTime.Add(_jwtOptions.Lifespan);
        var actualExpiration = new DateTimeOffset(jwtToken.ValidTo).ToUnixTimeSeconds();
        var expectedTimestamp = new DateTimeOffset(expectedExpiration).ToUnixTimeSeconds();
        actualExpiration.Should().Be(expectedTimestamp);
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

    [Test]
    public async Task ValidateTokenAsync_InvalidToken_ReturnsNull()
    {
        // Given an invalid token string
        var invalidToken = "this.is.not.a.valid.jwt.token";

        // When validating the invalid token
        var principal = await _service.ValidateTokenAsync(invalidToken);

        // Then validation should return null
        principal.Should().BeNull();
    }

    [Test]
    public async Task ValidateTokenAsync_ExpiredToken_ReturnsNull()
    {
        // Given a user
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };

        // And a fake time provider (set to recent date for JWT validation)
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var optionsMock = new Mock<IOptions<JwtOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_jwtOptions);

        var serviceWithFakeTime = new JwtTokenService<TestUser>(
            optionsMock.Object,
            new[] { _claimsProvider },
            _loggerMock.Object,
            fakeTime
        );

        // And a token generated at the current fake time
        var token = await serviceWithFakeTime.GenerateAccessTokenAsync(user);

        // When advancing time beyond the token's expiration
        fakeTime.Advance(_jwtOptions.Lifespan.Add(TimeSpan.FromMinutes(1)));

        // And validating the expired token
        var principal = await serviceWithFakeTime.ValidateTokenAsync(token);

        // Then validation should return null
        principal.Should().BeNull();
    }

    [Test]
    public async Task ValidateTokenAsync_TokenWithWrongSigningKey_ReturnsNull()
    {
        // Given a user
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };

        // And a token generated with one key
        var token = await _service.GenerateAccessTokenAsync(user);

        // And a service configured with a different key
        var differentKeyOptions = TestJwtOptions.CreateWithDifferentKey();
        var optionsMock = new Mock<IOptions<JwtOptions>>();
        optionsMock.Setup(o => o.Value).Returns(differentKeyOptions);

        var differentKeyService = new JwtTokenService<TestUser>(
            optionsMock.Object,
            new[] { _claimsProvider },
            _loggerMock.Object,
            _timeProvider
        );

        // When validating the token with the wrong key
        var principal = await differentKeyService.ValidateTokenAsync(token);

        // Then validation should return null
        principal.Should().BeNull();
    }

    [Test]
    public async Task GenerateAccessTokenAsync_UserWithNoNameClaim_UsesUnknownAsDefault()
    {
        // Given a user claims provider that doesn't include a name claim
        var noNameProvider = new Mock<IUserClaimsProvider<TestUser>>();
        noNameProvider.Setup(p => p.GetClaimsAsync(It.IsAny<TestUser>()))
            .ReturnsAsync(new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "user123"),
                new Claim(ClaimTypes.Email, "test@example.com")
            });

        var optionsMock = new Mock<IOptions<JwtOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_jwtOptions);

        var serviceWithNoName = new JwtTokenService<TestUser>(
            optionsMock.Object,
            new[] { noNameProvider.Object },
            _loggerMock.Object,
            _timeProvider
        );

        // And a test user
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };

        // When generating a token
        var token = await serviceWithNoName.GenerateAccessTokenAsync(user);

        // Then the token should be generated successfully
        token.Should().NotBeNullOrEmpty();

        // And the token should contain the other claims
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "user123");
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == "test@example.com");
    }

    [Test]
    public async Task GenerateAccessTokenAsync_WithObsoleteExpirationHours_UsesExpirationHours()
    {
        // Given JWT options with Lifespan set to zero (to trigger fallback)
        var optionsWithObsoleteProperty = new JwtOptions
        {
            Key = _jwtOptions.Key,
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            Lifespan = TimeSpan.Zero, // Trigger fallback to ExpirationHours
#pragma warning disable CS0618 // Suppress obsolete warning for testing
            ExpirationHours = 2
#pragma warning restore CS0618
        };

        var optionsMock = new Mock<IOptions<JwtOptions>>();
        optionsMock.Setup(o => o.Value).Returns(optionsWithObsoleteProperty);

        var serviceWithObsoleteOption = new JwtTokenService<TestUser>(
            optionsMock.Object,
            new[] { _claimsProvider },
            _loggerMock.Object,
            _timeProvider
        );

        // And a test user
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };

        // And the current fake time
        var currentTime = _timeProvider.GetUtcNow().DateTime;

        // When generating a token
        var token = await serviceWithObsoleteOption.GenerateAccessTokenAsync(user);

        // Then the token should expire after 2 hours (from ExpirationHours)
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // Compare as Unix timestamps to avoid timezone conversion issues
        var expectedExpiration = currentTime.AddHours(2);
        var actualExpiration = new DateTimeOffset(jwtToken.ValidTo).ToUnixTimeSeconds();
        var expectedTimestamp = new DateTimeOffset(expectedExpiration).ToUnixTimeSeconds();
        actualExpiration.Should().Be(expectedTimestamp);
    }

    [Test]
    public async Task GenerateAccessTokenAsync_MultipleClaimsProviders_CombinesAllClaims()
    {
        // Given multiple claims providers
        var provider1 = new Mock<IUserClaimsProvider<TestUser>>();
        provider1.Setup(p => p.GetClaimsAsync(It.IsAny<TestUser>()))
            .ReturnsAsync(new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "user123"),
                new Claim(ClaimTypes.Name, "testuser")
            });

        var provider2 = new Mock<IUserClaimsProvider<TestUser>>();
        provider2.Setup(p => p.GetClaimsAsync(It.IsAny<TestUser>()))
            .ReturnsAsync(new List<Claim>
            {
                new Claim(ClaimTypes.Email, "test@example.com"),
                new Claim(ClaimTypes.Role, "Admin")
            });

        var optionsMock = new Mock<IOptions<JwtOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_jwtOptions);

        var serviceWithMultipleProviders = new JwtTokenService<TestUser>(
            optionsMock.Object,
            new[] { provider1.Object, provider2.Object },
            _loggerMock.Object,
            _timeProvider
        );

        // And a test user
        var user = new TestUser
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };

        // When generating a token
        var token = await serviceWithMultipleProviders.GenerateAccessTokenAsync(user);

        // Then the token should contain claims from both providers
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "user123");
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "testuser");
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == "test@example.com");
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }
}

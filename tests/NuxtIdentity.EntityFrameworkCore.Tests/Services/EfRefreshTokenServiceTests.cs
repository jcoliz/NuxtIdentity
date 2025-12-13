using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuxtIdentity.Core.Configuration;
using NuxtIdentity.EntityFrameworkCore.Services;
using NuxtIdentity.EntityFrameworkCore.Tests.Helpers;

namespace NuxtIdentity.EntityFrameworkCore.Tests.Services;

[TestFixture]
[Category("Integration")]
public class EfRefreshTokenServiceTests
{
    private TestDbContext _context = null!;
    private EfRefreshTokenService<TestDbContext> _service = null!;
    private JwtOptions _jwtOptions = null!;

    [SetUp]
    public void SetUp()
    {
        // Given an in-memory database for testing
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new TestDbContext(options);

        // And JWT options configured for testing
        _jwtOptions = TestJwtOptions.CreateDefault();
        var optionsMock = new Mock<IOptions<JwtOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_jwtOptions);

        // And a logger
        var loggerMock = new Mock<ILogger<EfRefreshTokenService<TestDbContext>>>();

        _service = new EfRefreshTokenService<TestDbContext>(
            _context,
            loggerMock.Object,
            optionsMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
    }

    [Test]
    public async Task GenerateRefreshTokenAsync_ValidUserId_ReturnsNonEmptyToken()
    {
        // Given a valid user ID
        var userId = "user123";

        // When generating a refresh token
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // Then the token should not be empty
        token.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task GenerateRefreshTokenAsync_ValidUserId_TokenIsStoredInDatabase()
    {
        // Given a valid user ID
        var userId = "user123";

        // When generating a refresh token
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // Then a token entity should be stored in the database
        var tokenCount = await _context.RefreshTokens.CountAsync();
        tokenCount.Should().Be(1);

        // And the stored token should be for the correct user
        var storedToken = await _context.RefreshTokens.FirstAsync();
        storedToken.UserId.Should().Be(userId);
        storedToken.IsRevoked.Should().BeFalse();
    }

    [Test]
    public async Task GenerateRefreshTokenAsync_ValidUserId_TokenHasCorrectExpiration()
    {
        // Given a valid user ID
        var userId = "user123";
        var beforeGeneration = DateTime.UtcNow;

        // When generating a refresh token
        var token = await _service.GenerateRefreshTokenAsync(userId);
        var afterGeneration = DateTime.UtcNow;

        // Then the stored token should have expiration within expected range
        var storedToken = await _context.RefreshTokens.FirstAsync();
        var expectedExpiration = beforeGeneration.Add(_jwtOptions.RefreshTokenLifespan);
        var maxExpectedExpiration = afterGeneration.Add(_jwtOptions.RefreshTokenLifespan);

        storedToken.ExpiresAt.Should().BeOnOrAfter(expectedExpiration);
        storedToken.ExpiresAt.Should().BeOnOrBefore(maxExpectedExpiration);
    }

    [Test]
    public async Task GenerateRefreshTokenAsync_MultipleCalls_GeneratesUniqueTokens()
    {
        // Given a valid user ID
        var userId = "user123";

        // When generating multiple refresh tokens
        var token1 = await _service.GenerateRefreshTokenAsync(userId);
        var token2 = await _service.GenerateRefreshTokenAsync(userId);
        var token3 = await _service.GenerateRefreshTokenAsync(userId);

        // Then each token should be unique
        token1.Should().NotBe(token2);
        token2.Should().NotBe(token3);
        token1.Should().NotBe(token3);

        // And all tokens should be stored in the database
        var tokenCount = await _context.RefreshTokens.CountAsync();
        tokenCount.Should().Be(3);
    }

    [Test]
    public async Task ValidateRefreshTokenAsync_ValidToken_ReturnsTrue()
    {
        // Given a valid user ID
        var userId = "user123";
        // And a generated refresh token for that user
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // When validating the token with the correct user ID
        var isValid = await _service.ValidateRefreshTokenAsync(token, userId);

        // Then validation should succeed
        isValid.Should().BeTrue();
    }

    [Test]
    public async Task ValidateRefreshTokenAsync_NonExistentToken_ReturnsFalse()
    {
        // Given a valid user ID
        var userId = "user123";
        // And a token that was never generated
        var nonExistentToken = Convert.ToBase64String(new byte[64]);

        // When validating the non-existent token
        var isValid = await _service.ValidateRefreshTokenAsync(nonExistentToken, userId);

        // Then validation should fail
        isValid.Should().BeFalse();
    }

    [Test]
    public async Task ValidateRefreshTokenAsync_WrongUserId_ReturnsFalse()
    {
        // Given two different user IDs
        var userId1 = "user123";
        var userId2 = "user456";
        // And a token generated for the first user
        var token = await _service.GenerateRefreshTokenAsync(userId1);

        // When validating the token with the wrong user ID
        var isValid = await _service.ValidateRefreshTokenAsync(token, userId2);

        // Then validation should fail
        isValid.Should().BeFalse();
    }

    [Test]
    public async Task RevokeRefreshTokenAsync_ValidToken_TokenBecomesInvalid()
    {
        // Given a valid user ID
        var userId = "user123";
        // And a generated refresh token
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // And the token is valid before revocation
        var isValidBefore = await _service.ValidateRefreshTokenAsync(token, userId);
        isValidBefore.Should().BeTrue();

        // When revoking the token
        await _service.RevokeRefreshTokenAsync(token);

        // Then the token should become invalid
        var isValidAfter = await _service.ValidateRefreshTokenAsync(token, userId);
        isValidAfter.Should().BeFalse();

        // And the token should be marked as revoked in the database
        var storedToken = await _context.RefreshTokens.FirstAsync();
        storedToken.IsRevoked.Should().BeTrue();
    }

    [Test]
    public async Task RevokeAllUserTokensAsync_MultipleTokens_AllTokensBecomeInvalid()
    {
        // Given a valid user ID
        var userId = "user123";
        // And multiple refresh tokens for that user
        var token1 = await _service.GenerateRefreshTokenAsync(userId);
        var token2 = await _service.GenerateRefreshTokenAsync(userId);
        var token3 = await _service.GenerateRefreshTokenAsync(userId);

        // And all tokens are valid before revocation
        (await _service.ValidateRefreshTokenAsync(token1, userId)).Should().BeTrue();
        (await _service.ValidateRefreshTokenAsync(token2, userId)).Should().BeTrue();
        (await _service.ValidateRefreshTokenAsync(token3, userId)).Should().BeTrue();

        // When revoking all tokens for the user
        await _service.RevokeAllUserTokensAsync(userId);

        // Then all tokens should become invalid
        (await _service.ValidateRefreshTokenAsync(token1, userId)).Should().BeFalse();
        (await _service.ValidateRefreshTokenAsync(token2, userId)).Should().BeFalse();
        (await _service.ValidateRefreshTokenAsync(token3, userId)).Should().BeFalse();

        // And all tokens should be marked as revoked in the database
        var allTokens = await _context.RefreshTokens.ToListAsync();
        allTokens.Should().AllSatisfy(t => t.IsRevoked.Should().BeTrue());
    }

    [Test]
    public async Task RevokeAllUserTokensAsync_OnlyRevokesSpecificUserTokens()
    {
        // Given two different user IDs
        var user1Id = "user123";
        var user2Id = "user456";
        // And tokens for both users
        var user1Token = await _service.GenerateRefreshTokenAsync(user1Id);
        var user2Token = await _service.GenerateRefreshTokenAsync(user2Id);

        // Wait briefly for any background cleanup tasks to complete
        await Task.Delay(50);

        // When revoking all tokens for the first user
        await _service.RevokeAllUserTokensAsync(user1Id);

        // Then the first user's token should be invalid
        (await _service.ValidateRefreshTokenAsync(user1Token, user1Id)).Should().BeFalse();
        // And the second user's token should still be valid
        (await _service.ValidateRefreshTokenAsync(user2Token, user2Id)).Should().BeTrue();

        // And only the first user's tokens should be marked as revoked
        var user1Tokens = await _context.RefreshTokens.Where(t => t.UserId == user1Id).ToListAsync();
        var user2Tokens = await _context.RefreshTokens.Where(t => t.UserId == user2Id).ToListAsync();

        user1Tokens.Should().AllSatisfy(t => t.IsRevoked.Should().BeTrue());
        user2Tokens.Should().AllSatisfy(t => t.IsRevoked.Should().BeFalse());
    }

    [Test]
    public async Task GenerateRefreshTokenAsync_DifferentUsers_TokensAreIsolated()
    {
        // Given two different user IDs
        var user1Id = "user123";
        var user2Id = "user456";
        // And tokens generated for both users
        var user1Token = await _service.GenerateRefreshTokenAsync(user1Id);
        var user2Token = await _service.GenerateRefreshTokenAsync(user2Id);

        // When validating each token with its correct user
        // Then each user's token should be valid for that user
        (await _service.ValidateRefreshTokenAsync(user1Token, user1Id)).Should().BeTrue();
        // And invalid for the other user
        (await _service.ValidateRefreshTokenAsync(user1Token, user2Id)).Should().BeFalse();
        // And the second user's token should be valid for that user
        (await _service.ValidateRefreshTokenAsync(user2Token, user2Id)).Should().BeTrue();
        // And invalid for the first user
        (await _service.ValidateRefreshTokenAsync(user2Token, user1Id)).Should().BeFalse();
    }
}

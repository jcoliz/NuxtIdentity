using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
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
    private FakeTimeProvider _timeProvider = null!;
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

        // And a fake time provider initialized to current time for deterministic control
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        // And JWT options configured for testing
        _jwtOptions = TestJwtOptions.CreateDefault();
        var optionsMock = new Mock<IOptions<JwtOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_jwtOptions);

        // And a logger
        var loggerMock = new Mock<ILogger<EfRefreshTokenService<TestDbContext>>>();

        _service = new EfRefreshTokenService<TestDbContext>(
            _context,
            loggerMock.Object,
            optionsMock.Object,
            _timeProvider);
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
        var currentTime = _timeProvider.GetUtcNow().DateTime;

        // When generating a refresh token
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // Then the stored token should have the correct expiration
        var storedToken = await _context.RefreshTokens.FirstAsync();
        var expectedExpiration = currentTime.Add(_jwtOptions.RefreshTokenLifespan);

        storedToken.ExpiresAt.Should().Be(expectedExpiration);
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

    #region Error Cases

    [Test]
    public async Task ValidateRefreshTokenAsync_ExpiredToken_ReturnsFalse()
    {
        // Given a user ID
        var userId = "user123";

        // And a token generated at the current fake time
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // When advancing time beyond the token's expiration
        _timeProvider.Advance(_jwtOptions.RefreshTokenLifespan.Add(TimeSpan.FromMinutes(1)));

        // And validating the expired token
        var isValid = await _service.ValidateRefreshTokenAsync(token, userId);

        // Then validation should fail
        isValid.Should().BeFalse();
    }

    [Test]
    public async Task RevokeRefreshTokenAsync_NonExistentToken_DoesNotThrow()
    {
        // Given a token that was never generated
        var nonExistentToken = Convert.ToBase64String(new byte[64]);

        // When revoking the non-existent token
        Func<Task> act = async () => await _service.RevokeRefreshTokenAsync(nonExistentToken);

        // Then no exception should be thrown
        await act.Should().NotThrowAsync();

        // And no tokens should exist in the database
        var tokenCount = await _context.RefreshTokens.CountAsync();
        tokenCount.Should().Be(0);
    }

    [Test]
    public async Task GenerateRefreshTokenAsync_WithExpiredTokens_DeletesExpiredTokens()
    {
        // Given a user ID
        var userId = "user123";

        // And two tokens generated at the current fake time
        var expiredToken1 = await _service.GenerateRefreshTokenAsync(userId);
        var expiredToken2 = await _service.GenerateRefreshTokenAsync(userId);

        // Verify tokens are in database (should be 2)
        var tokenCountBefore = await _context.RefreshTokens.CountAsync();
        tokenCountBefore.Should().Be(2);

        // When advancing time beyond the tokens' expiration
        _timeProvider.Advance(_jwtOptions.RefreshTokenLifespan.Add(TimeSpan.FromMinutes(1)));

        // And generating a new token (which triggers cleanup)
        var newToken = await _service.GenerateRefreshTokenAsync(userId);

        // Then expired tokens should be deleted
        var tokensAfter = await _context.RefreshTokens.ToListAsync();

        // The expired tokens should be gone, only the new one remains
        tokensAfter.Should().HaveCount(1);
        tokensAfter[0].UserId.Should().Be(userId);
        tokensAfter[0].IsRevoked.Should().BeFalse();

        // And the new token should be valid
        (await _service.ValidateRefreshTokenAsync(newToken, userId)).Should().BeTrue();
    }

    [Test]
    public async Task RevokeRefreshTokenAsync_ExistingToken_UpdatesExpirationDate()
    {
        // Given a user ID
        var userId = "user123";
        // And a generated refresh token
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // Get the original expiration
        var tokenBefore = await _context.RefreshTokens.FirstAsync();
        var originalExpiration = tokenBefore.ExpiresAt;

        var currentTime = _timeProvider.GetUtcNow().DateTime;

        // When revoking the token
        await _service.RevokeRefreshTokenAsync(token);

        // Then the expiration should be updated to 7 days from current time
        var tokenAfter = await _context.RefreshTokens.FirstAsync();
        var expectedExpiration = currentTime.AddDays(7);

        tokenAfter.ExpiresAt.Should().Be(expectedExpiration);
        // Use BeOnOrAfter since revocation happens at the same fake time
        tokenAfter.ExpiresAt.Should().BeOnOrAfter(originalExpiration);
    }

    [Test]
    public async Task ValidateRefreshTokenAsync_RevokedToken_ReturnsFalse()
    {
        // Given a user ID
        var userId = "user123";
        // And a generated refresh token
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // And the token is revoked
        await _service.RevokeRefreshTokenAsync(token);

        // When validating the revoked token
        var isValid = await _service.ValidateRefreshTokenAsync(token, userId);

        // Then validation should fail
        isValid.Should().BeFalse();

        // And the token should be marked as revoked in the database
        var storedToken = await _context.RefreshTokens.FirstAsync();
        storedToken.IsRevoked.Should().BeTrue();
    }

    [Test]
    public async Task GenerateRefreshTokenAsync_StoresHashedTokenNotPlaintext()
    {
        // Given a user ID
        var userId = "user123";

        // When generating a refresh token
        var plainTextToken = await _service.GenerateRefreshTokenAsync(userId);

        // Then the stored token hash should not match the plaintext token
        var storedToken = await _context.RefreshTokens.FirstAsync();
        storedToken.TokenHash.Should().NotBe(plainTextToken);

        // And the token hash should be base64 encoded (characteristic of SHA256)
        storedToken.TokenHash.Should().MatchRegex("^[A-Za-z0-9+/=]+$");
    }

    #endregion
}

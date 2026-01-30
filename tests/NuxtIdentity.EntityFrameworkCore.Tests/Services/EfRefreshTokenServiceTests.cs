using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NuxtIdentity.Core.Configuration;
using NuxtIdentity.EntityFrameworkCore.Extensions;
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
    public async Task ValidateRefreshTokenAsync_ValidToken_ReturnsUserId()
    {
        // Given a valid user ID
        var userId = "user123";
        // And a generated refresh token for that user
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // When validating the token
        var returnedUserId = await _service.ValidateRefreshTokenAsync(token);

        // Then validation should return the user ID
        returnedUserId.Should().Be(userId);
    }

    [Test]
    public async Task ValidateRefreshTokenAsync_NonExistentToken_ReturnsNull()
    {
        // Given a token that was never generated
        var nonExistentToken = Convert.ToBase64String(new byte[64]);

        // When validating the non-existent token
        var returnedUserId = await _service.ValidateRefreshTokenAsync(nonExistentToken);

        // Then validation should return null
        returnedUserId.Should().BeNull();
    }

    [Test]
    public async Task RevokeRefreshTokenAsync_ValidToken_TokenBecomesInvalid()
    {
        // Given a valid user ID
        var userId = "user123";
        // And a generated refresh token
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // And the token is valid before revocation
        var userIdBefore = await _service.ValidateRefreshTokenAsync(token);
        userIdBefore.Should().Be(userId);

        // When revoking the token
        await _service.RevokeRefreshTokenAsync(token);

        // Then the token should become invalid
        var userIdAfter = await _service.ValidateRefreshTokenAsync(token);
        userIdAfter.Should().BeNull();

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
        (await _service.ValidateRefreshTokenAsync(token1)).Should().Be(userId);
        (await _service.ValidateRefreshTokenAsync(token2)).Should().Be(userId);
        (await _service.ValidateRefreshTokenAsync(token3)).Should().Be(userId);

        // When revoking all tokens for the user
        await _service.RevokeAllUserTokensAsync(userId);

        // Then all tokens should become invalid
        (await _service.ValidateRefreshTokenAsync(token1)).Should().BeNull();
        (await _service.ValidateRefreshTokenAsync(token2)).Should().BeNull();
        (await _service.ValidateRefreshTokenAsync(token3)).Should().BeNull();

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
        (await _service.ValidateRefreshTokenAsync(user1Token)).Should().BeNull();
        // And the second user's token should still be valid
        (await _service.ValidateRefreshTokenAsync(user2Token)).Should().Be(user2Id);

        // And only the first user's tokens should be marked as revoked
        var user1Tokens = await _context.RefreshTokens.Where(t => t.UserId == user1Id).ToListAsync();
        var user2Tokens = await _context.RefreshTokens.Where(t => t.UserId == user2Id).ToListAsync();

        user1Tokens.Should().AllSatisfy(t => t.IsRevoked.Should().BeTrue());
        user2Tokens.Should().AllSatisfy(t => t.IsRevoked.Should().BeFalse());
    }

    [Test]
    public async Task GenerateRefreshTokenAsync_DifferentUsers_TokensReturnCorrectUserId()
    {
        // Given two different user IDs
        var user1Id = "user123";
        var user2Id = "user456";
        // And tokens generated for both users
        var user1Token = await _service.GenerateRefreshTokenAsync(user1Id);
        var user2Token = await _service.GenerateRefreshTokenAsync(user2Id);

        // When validating each token
        // Then each token should return its associated user ID
        (await _service.ValidateRefreshTokenAsync(user1Token)).Should().Be(user1Id);
        (await _service.ValidateRefreshTokenAsync(user2Token)).Should().Be(user2Id);
    }

    #region Error Cases

    [Test]
    public async Task ValidateRefreshTokenAsync_ExpiredToken_ReturnsNull()
    {
        // Given a user ID
        var userId = "user123";

        // And a token generated at the current fake time
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // When advancing time beyond the token's expiration
        _timeProvider.Advance(_jwtOptions.RefreshTokenLifespan.Add(TimeSpan.FromMinutes(1)));

        // And validating the expired token
        var returnedUserId = await _service.ValidateRefreshTokenAsync(token);

        // Then validation should return null
        returnedUserId.Should().BeNull();
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
        (await _service.ValidateRefreshTokenAsync(newToken)).Should().Be(userId);
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
    public async Task ValidateRefreshTokenAsync_RevokedToken_ReturnsNull()
    {
        // Given a user ID
        var userId = "user123";
        // And a generated refresh token
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // And the token is revoked
        await _service.RevokeRefreshTokenAsync(token);

        // When validating the revoked token
        var returnedUserId = await _service.ValidateRefreshTokenAsync(token);

        // Then validation should return null
        returnedUserId.Should().BeNull();

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

    [Test]
    public async Task GenerateRefreshTokenAsync_ValidUserId_ReturnsBase64String()
    {
        // Given a valid user ID
        var userId = "user123";

        // When generating a refresh token
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // Then the token should not be empty
        token.Should().NotBeNullOrEmpty();

        // And it should be a valid base64 string
        Action act = () => Convert.FromBase64String(token);
        act.Should().NotThrow();
    }

    [Test]
    public async Task RevokeAllUserTokensAsync_NonExistentUser_DoesNotThrow()
    {
        // Given a user ID that has no tokens
        var nonExistentUserId = "nonexistent";

        // When attempting to revoke all tokens for that user
        Func<Task> act = async () => await _service.RevokeAllUserTokensAsync(nonExistentUserId);

        // Then no exception should be thrown
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// Tests that token generation succeeds even when cleanup of expired tokens fails.
    /// This test covers line 83 where cleanup failures are caught and logged.
    ///
    /// NOTE: This is a convoluted test that uses a custom FaultyDbContext to simulate
    /// database failures during cleanup. If this test starts failing or becomes problematic,
    /// feel free to disable it rather than spending significant time debugging.
    /// </summary>
    [Test]
    public async Task GenerateRefreshTokenAsync_CleanupFails_StillReturnsToken()
    {
        // Given a userId
        var userId = "user123";

        // And a faulty DbContext that throws during the cleanup SaveChangesAsync
        var faultyContext = new FaultyDbContext(new DbContextOptionsBuilder<FaultyDbContext>()
            .UseInMemoryDatabase(databaseName: $"FaultyDb_{Guid.NewGuid()}")
            .Options);

        // And an expired token exists in the faulty context
        var expiredToken = "old-token";
        var pastTime = _timeProvider.GetUtcNow().AddDays(-_jwtOptions.RefreshTokenLifespan.TotalDays - 1);
        faultyContext.RefreshTokens.Add(new Core.Models.RefreshTokenEntity
        {
            TokenHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(expiredToken))),
            UserId = userId,
            ExpiresAt = pastTime.DateTime,
            CreatedAt = pastTime.DateTime,
            IsRevoked = false
        });
        await faultyContext.SaveChangesAsync();

        // Verify the expired token is in the database
        var expiredCount = await faultyContext.RefreshTokens.CountAsync();
        expiredCount.Should().Be(1, "we should have 1 expired token before generating a new one");

        var optionsMock = new Mock<IOptions<JwtOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_jwtOptions);

        var loggerMock = new Mock<ILogger<EfRefreshTokenService<FaultyDbContext>>>();

        var serviceWithFaultyContext = new EfRefreshTokenService<FaultyDbContext>(
            faultyContext,
            loggerMock.Object,
            optionsMock.Object,
            _timeProvider);

        // When generating a refresh token (which triggers cleanup that will fail)
        var token = await serviceWithFaultyContext.GenerateRefreshTokenAsync(userId);

        // Then the token should still be generated successfully despite cleanup failure
        token.Should().NotBeNullOrEmpty();

        // And the new token should be stored in the database
        var newTokenCount = await faultyContext.RefreshTokens.CountAsync();
        newTokenCount.Should().Be(2, "new token should be saved even though cleanup failed");

        // And the expired token should still exist (cleanup failed)
        var expiredTokenEntity = await faultyContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.ExpiresAt < _timeProvider.GetUtcNow().DateTime);
        expiredTokenEntity.Should().NotBeNull("expired token should still exist because cleanup failed");
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Faulty DbContext that throws during SaveChangesAsync in cleanup scenarios.
    /// </summary>
    public class FaultyDbContext : DbContext
    {
        public FaultyDbContext(DbContextOptions<FaultyDbContext> options) : base(options)
        {
        }

        public DbSet<Core.Models.RefreshTokenEntity> RefreshTokens => Set<Core.Models.RefreshTokenEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Apply the same configuration as TestDbContext
            modelBuilder.ConfigureNuxtIdentityRefreshTokens();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Check if we're removing entities (cleanup phase)
            var entriesToRemove = ChangeTracker.Entries<Core.Models.RefreshTokenEntity>()
                .Where(e => e.State == EntityState.Deleted)
                .ToList();

            // If we're deleting tokens, this is the cleanup phase - throw exception
            if (entriesToRemove.Any())
            {
                throw new InvalidOperationException("Simulated database failure during cleanup");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    #endregion
}

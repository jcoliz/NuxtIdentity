using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NUnit.Framework;
using NuxtIdentity.Core.Configuration;
using NuxtIdentity.Core.Services;
using NuxtIdentity.Core.Tests.Helpers;

namespace NuxtIdentity.Core.Tests.Services;

[TestFixture]
[Category("Unit")]
public class InMemoryRefreshTokenServiceTests
{
    private JwtOptions _jwtOptions = null!;
    private FakeTimeProvider _timeProvider = null!;
    private InMemoryRefreshTokenService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _jwtOptions = TestJwtOptions.CreateDefault();
        _timeProvider = new FakeTimeProvider();
        var optionsMock = new Mock<IOptions<JwtOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_jwtOptions);

        _service = new InMemoryRefreshTokenService(optionsMock.Object, _timeProvider);
    }

    [Test]
    public async Task GenerateRefreshTokenAsync_ValidUserId_ReturnsNonEmptyToken()
    {
        // Given a valid user ID
        var userId = "user123";

        // When generating a refresh token
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // Then the token should not be empty
        Assert.That(token, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task GenerateRefreshTokenAsync_ValidUserId_ReturnsKeyDotSecretFormat()
    {
        // Given a valid user ID
        var userId = "user123";

        // When generating a refresh token
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // Then the token should not be empty
        Assert.That(token, Is.Not.Null.And.Not.Empty);

        // And it should be in the format {GUID}.{base64}
        var dotIndex = token.IndexOf('.');
        Assert.That(dotIndex, Is.EqualTo(36), "GUID is 36 characters, followed by a period");

        // And the first part should be a valid GUID
        var guidPart = token[..36];
        Assert.That(Guid.TryParse(guidPart, out _), Is.True, "first part should be a valid GUID");

        // And the second part should be a valid base64 string
        var secretPart = token[(dotIndex + 1)..];
        Assert.That(() => Convert.FromBase64String(secretPart), Throws.Nothing);
    }

    [Test]
    public async Task GenerateRefreshTokenAsync_MultipleCalls_ReturnsUniqueTokens()
    {
        // Given a valid user ID
        var userId = "user123";

        // When generating multiple refresh tokens
        var token1 = await _service.GenerateRefreshTokenAsync(userId);
        var token2 = await _service.GenerateRefreshTokenAsync(userId);
        var token3 = await _service.GenerateRefreshTokenAsync(userId);

        // Then each token should be unique
        Assert.That(token1, Is.Not.EqualTo(token2));
        Assert.That(token2, Is.Not.EqualTo(token3));
        Assert.That(token1, Is.Not.EqualTo(token3));
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
        Assert.That(returnedUserId, Is.EqualTo(userId));
    }

    [Test]
    public async Task ValidateRefreshTokenAsync_NonExistentToken_ReturnsNull()
    {
        // Given a token that was never generated
        var nonExistentToken = Convert.ToBase64String(new byte[64]);

        // When validating the non-existent token
        var returnedUserId = await _service.ValidateRefreshTokenAsync(nonExistentToken);

        // Then validation should return null
        Assert.That(returnedUserId, Is.Null);
    }

    [Test]
    public async Task ValidateRefreshTokenAsync_RevokedToken_ReturnsNull()
    {
        // Given a valid user ID
        var userId = "user123";
        // And a generated refresh token
        var token = await _service.GenerateRefreshTokenAsync(userId);
        // And the token has been revoked
        await _service.RevokeRefreshTokenAsync(token);

        // When validating the revoked token
        var returnedUserId = await _service.ValidateRefreshTokenAsync(token);

        // Then validation should return null
        Assert.That(returnedUserId, Is.Null);
    }

    [Test]
    public async Task ValidateRefreshTokenAsync_ExpiredToken_ReturnsNull()
    {
        // Given a fake time provider
        var fakeTime = new FakeTimeProvider();
        var optionsMock = new Mock<IOptions<JwtOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_jwtOptions);
        var serviceWithFakeTime = new InMemoryRefreshTokenService(optionsMock.Object, fakeTime);

        // And a valid user ID
        var userId = "user123";
        // And a generated refresh token at the current fake time
        var token = await serviceWithFakeTime.GenerateRefreshTokenAsync(userId);

        // When advancing time beyond the token's expiration
        fakeTime.Advance(_jwtOptions.RefreshTokenLifespan.Add(TimeSpan.FromMinutes(1)));

        // And validating the expired token
        var returnedUserId = await serviceWithFakeTime.ValidateRefreshTokenAsync(token);

        // Then validation should return null
        Assert.That(returnedUserId, Is.Null);
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
        Assert.That(userIdBefore, Is.EqualTo(userId));

        // When revoking the token
        await _service.RevokeRefreshTokenAsync(token);

        // Then the token should become invalid
        var userIdAfter = await _service.ValidateRefreshTokenAsync(token);
        Assert.That(userIdAfter, Is.Null);
    }

    [Test]
    public void RevokeRefreshTokenAsync_NonExistentToken_DoesNotThrow()
    {
        // Given a token that was never generated
        var nonExistentToken = Convert.ToBase64String(new byte[64]);

        // When attempting to revoke the non-existent token
        Func<Task> act = async () => await _service.RevokeRefreshTokenAsync(nonExistentToken);

        // Then no exception should be thrown
        Assert.That(act, Throws.Nothing);
    }

    [Test]
    public async Task RevokeAllUserTokensAsync_MultipleTokens_AllTokensBecomesInvalid()
    {
        // Given a valid user ID
        var userId = "user123";
        // And multiple refresh tokens for that user
        var token1 = await _service.GenerateRefreshTokenAsync(userId);
        var token2 = await _service.GenerateRefreshTokenAsync(userId);
        var token3 = await _service.GenerateRefreshTokenAsync(userId);

        // And all tokens are valid before revocation
        Assert.That(await _service.ValidateRefreshTokenAsync(token1), Is.EqualTo(userId));
        Assert.That(await _service.ValidateRefreshTokenAsync(token2), Is.EqualTo(userId));
        Assert.That(await _service.ValidateRefreshTokenAsync(token3), Is.EqualTo(userId));

        // When revoking all tokens for the user
        await _service.RevokeAllUserTokensAsync(userId);

        // Then all tokens should become invalid
        Assert.That(await _service.ValidateRefreshTokenAsync(token1), Is.Null);
        Assert.That(await _service.ValidateRefreshTokenAsync(token2), Is.Null);
        Assert.That(await _service.ValidateRefreshTokenAsync(token3), Is.Null);
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
        Assert.That(await _service.ValidateRefreshTokenAsync(user1Token), Is.Null);
        // And the second user's token should still be valid
        Assert.That(await _service.ValidateRefreshTokenAsync(user2Token), Is.EqualTo(user2Id));
    }

    [Test]
    public void RevokeAllUserTokensAsync_NonExistentUser_DoesNotThrow()
    {
        // Given a user ID that has no tokens
        var nonExistentUserId = "nonexistent";

        // When attempting to revoke all tokens for that user
        Func<Task> act = async () => await _service.RevokeAllUserTokensAsync(nonExistentUserId);

        // Then no exception should be thrown
        Assert.That(act, Throws.Nothing);
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
        Assert.That(await _service.ValidateRefreshTokenAsync(user1Token), Is.EqualTo(user1Id));
        Assert.That(await _service.ValidateRefreshTokenAsync(user2Token), Is.EqualTo(user2Id));
    }

    [Test]
    public async Task GetUsersLoggedInRecentlyAsync_ReturnsUsersOrderedByMostRecentLogin()
    {
        // Given two users with multiple login events at different times
        var user1Id = "user123";
        var user2Id = "user456";

        await _service.GenerateRefreshTokenAsync(user1Id);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        await _service.GenerateRefreshTokenAsync(user2Id);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        var expectedUser1LastLogin = _timeProvider.GetUtcNow().UtcDateTime;
        await _service.GenerateRefreshTokenAsync(user1Id);

        // When querying users logged in recently
        var recentUsers = await _service.GetUsersLoggedInRecentlyAsync();

        // Then users should be deduplicated and ordered by recency
        Assert.That(recentUsers.Count, Is.EqualTo(2));
        Assert.That(recentUsers[0].UserId, Is.EqualTo(user1Id));
        Assert.That(recentUsers[0].LastLoginAt, Is.EqualTo(expectedUser1LastLogin));
        Assert.That(recentUsers[1].UserId, Is.EqualTo(user2Id));
    }
}

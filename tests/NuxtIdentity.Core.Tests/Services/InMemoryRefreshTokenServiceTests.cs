using FluentAssertions;
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
        token.Should().NotBeNullOrEmpty();
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
    public async Task GenerateRefreshTokenAsync_MultipleCalls_ReturnsUniqueTokens()
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
    public async Task ValidateRefreshTokenAsync_RevokedToken_ReturnsFalse()
    {
        // Given a valid user ID
        var userId = "user123";
        // And a generated refresh token
        var token = await _service.GenerateRefreshTokenAsync(userId);
        // And the token has been revoked
        await _service.RevokeRefreshTokenAsync(token);

        // When validating the revoked token
        var isValid = await _service.ValidateRefreshTokenAsync(token, userId);

        // Then validation should fail
        isValid.Should().BeFalse();
    }

    [Test]
    public async Task ValidateRefreshTokenAsync_ExpiredToken_ReturnsFalse()
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
        var isValid = await serviceWithFakeTime.ValidateRefreshTokenAsync(token, userId);

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
    }

    [Test]
    public async Task RevokeRefreshTokenAsync_NonExistentToken_DoesNotThrow()
    {
        // Given a token that was never generated
        var nonExistentToken = Convert.ToBase64String(new byte[64]);

        // When attempting to revoke the non-existent token
        Func<Task> act = async () => await _service.RevokeRefreshTokenAsync(nonExistentToken);

        // Then no exception should be thrown
        await act.Should().NotThrowAsync();
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
        (await _service.ValidateRefreshTokenAsync(token1, userId)).Should().BeTrue();
        (await _service.ValidateRefreshTokenAsync(token2, userId)).Should().BeTrue();
        (await _service.ValidateRefreshTokenAsync(token3, userId)).Should().BeTrue();

        // When revoking all tokens for the user
        await _service.RevokeAllUserTokensAsync(userId);

        // Then all tokens should become invalid
        (await _service.ValidateRefreshTokenAsync(token1, userId)).Should().BeFalse();
        (await _service.ValidateRefreshTokenAsync(token2, userId)).Should().BeFalse();
        (await _service.ValidateRefreshTokenAsync(token3, userId)).Should().BeFalse();
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

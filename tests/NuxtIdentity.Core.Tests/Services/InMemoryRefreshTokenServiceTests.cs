using FluentAssertions;
using Microsoft.Extensions.Options;
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
    private InMemoryRefreshTokenService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _jwtOptions = TestJwtOptions.CreateDefault();
        var optionsMock = new Mock<IOptions<JwtOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_jwtOptions);

        _service = new InMemoryRefreshTokenService(optionsMock.Object);
    }

    [Test]
    public async Task GenerateRefreshTokenAsync_ValidUserId_ReturnsNonEmptyToken()
    {
        // Arrange
        var userId = "user123";

        // Act
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // Assert
        token.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task GenerateRefreshTokenAsync_ValidUserId_ReturnsBase64String()
    {
        // Arrange
        var userId = "user123";

        // Act
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // Assert
        token.Should().NotBeNullOrEmpty();

        // Verify it's valid base64
        Action act = () => Convert.FromBase64String(token);
        act.Should().NotThrow();
    }

    [Test]
    public async Task GenerateRefreshTokenAsync_MultipleCalls_ReturnsUniqueTokens()
    {
        // Arrange
        var userId = "user123";

        // Act
        var token1 = await _service.GenerateRefreshTokenAsync(userId);
        var token2 = await _service.GenerateRefreshTokenAsync(userId);
        var token3 = await _service.GenerateRefreshTokenAsync(userId);

        // Assert
        token1.Should().NotBe(token2);
        token2.Should().NotBe(token3);
        token1.Should().NotBe(token3);
    }

    [Test]
    public async Task ValidateRefreshTokenAsync_ValidToken_ReturnsTrue()
    {
        // Arrange
        var userId = "user123";
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // Act
        var isValid = await _service.ValidateRefreshTokenAsync(token, userId);

        // Assert
        isValid.Should().BeTrue();
    }

    [Test]
    public async Task ValidateRefreshTokenAsync_NonExistentToken_ReturnsFalse()
    {
        // Arrange
        var userId = "user123";
        var nonExistentToken = Convert.ToBase64String(new byte[64]);

        // Act
        var isValid = await _service.ValidateRefreshTokenAsync(nonExistentToken, userId);

        // Assert
        isValid.Should().BeFalse();
    }

    [Test]
    public async Task ValidateRefreshTokenAsync_WrongUserId_ReturnsFalse()
    {
        // Arrange
        var userId1 = "user123";
        var userId2 = "user456";
        var token = await _service.GenerateRefreshTokenAsync(userId1);

        // Act
        var isValid = await _service.ValidateRefreshTokenAsync(token, userId2);

        // Assert
        isValid.Should().BeFalse();
    }

    [Test]
    public async Task ValidateRefreshTokenAsync_RevokedToken_ReturnsFalse()
    {
        // Arrange
        var userId = "user123";
        var token = await _service.GenerateRefreshTokenAsync(userId);
        await _service.RevokeRefreshTokenAsync(token);

        // Act
        var isValid = await _service.ValidateRefreshTokenAsync(token, userId);

        // Assert
        isValid.Should().BeFalse();
    }

    [Test]
    public async Task RevokeRefreshTokenAsync_ValidToken_TokenBecomesInvalid()
    {
        // Arrange
        var userId = "user123";
        var token = await _service.GenerateRefreshTokenAsync(userId);

        // Verify token is valid before revocation
        var isValidBefore = await _service.ValidateRefreshTokenAsync(token, userId);
        isValidBefore.Should().BeTrue();

        // Act
        await _service.RevokeRefreshTokenAsync(token);

        // Assert
        var isValidAfter = await _service.ValidateRefreshTokenAsync(token, userId);
        isValidAfter.Should().BeFalse();
    }

    [Test]
    public async Task RevokeRefreshTokenAsync_NonExistentToken_DoesNotThrow()
    {
        // Arrange
        var nonExistentToken = Convert.ToBase64String(new byte[64]);

        // Act
        Func<Task> act = async () => await _service.RevokeRefreshTokenAsync(nonExistentToken);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task RevokeAllUserTokensAsync_MultipleTokens_AllTokensBecomesInvalid()
    {
        // Arrange
        var userId = "user123";
        var token1 = await _service.GenerateRefreshTokenAsync(userId);
        var token2 = await _service.GenerateRefreshTokenAsync(userId);
        var token3 = await _service.GenerateRefreshTokenAsync(userId);

        // Verify all tokens are valid before revocation
        (await _service.ValidateRefreshTokenAsync(token1, userId)).Should().BeTrue();
        (await _service.ValidateRefreshTokenAsync(token2, userId)).Should().BeTrue();
        (await _service.ValidateRefreshTokenAsync(token3, userId)).Should().BeTrue();

        // Act
        await _service.RevokeAllUserTokensAsync(userId);

        // Assert
        (await _service.ValidateRefreshTokenAsync(token1, userId)).Should().BeFalse();
        (await _service.ValidateRefreshTokenAsync(token2, userId)).Should().BeFalse();
        (await _service.ValidateRefreshTokenAsync(token3, userId)).Should().BeFalse();
    }

    [Test]
    public async Task RevokeAllUserTokensAsync_OnlyRevokesSpecificUserTokens()
    {
        // Arrange
        var user1Id = "user123";
        var user2Id = "user456";
        var user1Token = await _service.GenerateRefreshTokenAsync(user1Id);
        var user2Token = await _service.GenerateRefreshTokenAsync(user2Id);

        // Act
        await _service.RevokeAllUserTokensAsync(user1Id);

        // Assert
        (await _service.ValidateRefreshTokenAsync(user1Token, user1Id)).Should().BeFalse();
        (await _service.ValidateRefreshTokenAsync(user2Token, user2Id)).Should().BeTrue();
    }

    [Test]
    public async Task RevokeAllUserTokensAsync_NonExistentUser_DoesNotThrow()
    {
        // Arrange
        var nonExistentUserId = "nonexistent";

        // Act
        Func<Task> act = async () => await _service.RevokeAllUserTokensAsync(nonExistentUserId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task GenerateRefreshTokenAsync_DifferentUsers_TokensAreIsolated()
    {
        // Arrange
        var user1Id = "user123";
        var user2Id = "user456";
        var user1Token = await _service.GenerateRefreshTokenAsync(user1Id);
        var user2Token = await _service.GenerateRefreshTokenAsync(user2Id);

        // Act & Assert
        (await _service.ValidateRefreshTokenAsync(user1Token, user1Id)).Should().BeTrue();
        (await _service.ValidateRefreshTokenAsync(user1Token, user2Id)).Should().BeFalse();
        (await _service.ValidateRefreshTokenAsync(user2Token, user2Id)).Should().BeTrue();
        (await _service.ValidateRefreshTokenAsync(user2Token, user1Id)).Should().BeFalse();
    }
}

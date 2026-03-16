using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using NuxtIdentity.Core.Models;
using NuxtIdentity.Core.Services;
using NuxtIdentity.Core.Tests.Helpers;

namespace NuxtIdentity.Core.Tests.Services;

/// <summary>
/// Unit tests for <see cref="InMemoryUserNotifier{TUser}"/>.
/// </summary>
[TestFixture]
[Category("Unit")]
public class InMemoryUserNotifierTests
{
    private FakeTimeProvider _timeProvider = null!;
    private InMemoryUserNotifier<TestUser> _notifier = null!;

    [SetUp]
    public void SetUp()
    {
        _timeProvider = new FakeTimeProvider();
        _notifier = new InMemoryUserNotifier<TestUser>(_timeProvider);
    }

    [Test]
    public async Task SendResetCodeAsync_CapturesNotification()
    {
        // Given: A user and a reset code
        var user = new TestUser { Id = "user1", Username = "testuser", Email = "test@example.com" };
        var resetCode = "abc123";

        // When: A reset code notification is sent
        await _notifier.SendResetCodeAsync(user, resetCode);

        // Then: The notification should be captured
        var notifications = await _notifier.GetNotificationsAsync();
        notifications.Should().HaveCount(1);

        // And: The notification should contain the correct data
        notifications[0].Code.Should().Be(resetCode);
        notifications[0].Type.Should().Be(NotificationType.PasswordReset);
        notifications[0].Timestamp.Should().Be(_timeProvider.GetUtcNow().DateTime);
    }

    [Test]
    public async Task SendEmailConfirmationAsync_CapturesNotification()
    {
        // Given: A user and a confirmation code
        var user = new TestUser { Id = "user1", Username = "testuser", Email = "test@example.com" };
        var confirmationCode = "confirm456";

        // When: An email confirmation notification is sent
        await _notifier.SendEmailConfirmationAsync(user, confirmationCode);

        // Then: The notification should be captured
        var notifications = await _notifier.GetNotificationsAsync();
        notifications.Should().HaveCount(1);

        // And: The notification should contain the correct data
        notifications[0].Code.Should().Be(confirmationCode);
        notifications[0].Type.Should().Be(NotificationType.EmailConfirmation);
    }

    [Test]
    public async Task GetNotificationsAsync_MultipleNotifications_ReturnsAll()
    {
        // Given: A user with multiple notifications sent
        var user = new TestUser { Id = "user1", Username = "testuser", Email = "test@example.com" };
        await _notifier.SendResetCodeAsync(user, "reset1");
        await _notifier.SendEmailConfirmationAsync(user, "confirm1");
        await _notifier.SendResetCodeAsync(user, "reset2");

        // When: Retrieving all notifications
        var notifications = await _notifier.GetNotificationsAsync();

        // Then: All notifications should be returned in order
        notifications.Should().HaveCount(3);
        notifications[0].Code.Should().Be("reset1");
        notifications[0].Type.Should().Be(NotificationType.PasswordReset);
        notifications[1].Code.Should().Be("confirm1");
        notifications[1].Type.Should().Be(NotificationType.EmailConfirmation);
        notifications[2].Code.Should().Be("reset2");
        notifications[2].Type.Should().Be(NotificationType.PasswordReset);
    }

    [Test]
    public async Task ClearAsync_RemovesAllNotifications()
    {
        // Given: Notifications have been captured
        var user = new TestUser { Id = "user1", Username = "testuser", Email = "test@example.com" };
        await _notifier.SendResetCodeAsync(user, "reset1");
        await _notifier.SendEmailConfirmationAsync(user, "confirm1");

        // When: Clearing all notifications
        await _notifier.ClearAsync();

        // Then: No notifications should remain
        var notifications = await _notifier.GetNotificationsAsync();
        notifications.Should().BeEmpty();
    }
}

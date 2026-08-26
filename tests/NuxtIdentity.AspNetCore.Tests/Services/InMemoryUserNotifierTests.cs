using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using NuxtIdentity.AspNetCore.Services;
using NuxtIdentity.AspNetCore.Tests.Helpers;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.AspNetCore.Tests.Services;

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
        var user = new TestUser("testuser") { Id = "user1" };
        var resetCode = "abc123";

        // When: A reset code notification is sent
        await _notifier.SendResetCodeAsync(user, resetCode);

        // Then: The notification should be captured
        var notifications = await _notifier.GetNotificationsAsync();
        Assert.That(notifications, Has.Count.EqualTo(1));

        // And: The notification should contain the correct data
        Assert.That(notifications[0].Code, Is.EqualTo(resetCode));
        Assert.That(notifications[0].Type, Is.EqualTo(NotificationType.PasswordReset));
        Assert.That(notifications[0].Timestamp, Is.EqualTo(_timeProvider.GetUtcNow().DateTime));
    }

    [Test]
    public async Task SendResetCodeAsync_CapturesUserId()
    {
        // Given: A user with a known ID and a reset code
        var user = new TestUser("testuser") { Id = "user1" };
        var resetCode = "abc123";

        // When: A reset code notification is sent
        await _notifier.SendResetCodeAsync(user, resetCode);

        // Then: The notification should capture the user's identity
        var notifications = await _notifier.GetNotificationsAsync();
        Assert.That(notifications[0].UserId, Is.EqualTo("user1"));
        Assert.That(notifications[0].UserName, Is.EqualTo("testuser"));
    }

    [Test]
    public async Task SendEmailConfirmationAsync_CapturesNotification()
    {
        // Given: A user and a confirmation code
        var user = new TestUser("testuser") { Id = "user1" };
        var confirmationCode = "confirm456";

        // When: An email confirmation notification is sent
        await _notifier.SendEmailConfirmationAsync(user, confirmationCode);

        // Then: The notification should be captured
        var notifications = await _notifier.GetNotificationsAsync();
        Assert.That(notifications, Has.Count.EqualTo(1));

        // And: The notification should contain the correct data
        Assert.That(notifications[0].Code, Is.EqualTo(confirmationCode));
        Assert.That(notifications[0].Type, Is.EqualTo(NotificationType.EmailConfirmation));
    }

    [Test]
    public async Task SendEmailConfirmationAsync_CapturesUserId()
    {
        // Given: A user with a known ID and a confirmation code
        var user = new TestUser("confirmuser") { Id = "user2" };
        var confirmationCode = "confirm456";

        // When: An email confirmation notification is sent
        await _notifier.SendEmailConfirmationAsync(user, confirmationCode);

        // Then: The notification should capture the user's identity
        var notifications = await _notifier.GetNotificationsAsync();
        Assert.That(notifications[0].UserId, Is.EqualTo("user2"));
        Assert.That(notifications[0].UserName, Is.EqualTo("confirmuser"));
    }

    [Test]
    public async Task GetNotificationsAsync_MultipleNotifications_ReturnsAll()
    {
        // Given: A user with multiple notifications sent
        var user = new TestUser("testuser") { Id = "user1" };
        await _notifier.SendResetCodeAsync(user, "reset1");
        await _notifier.SendEmailConfirmationAsync(user, "confirm1");
        await _notifier.SendResetCodeAsync(user, "reset2");

        // When: Retrieving all notifications
        var notifications = await _notifier.GetNotificationsAsync();

        // Then: All notifications should be returned in order
        Assert.That(notifications, Has.Count.EqualTo(3));
        Assert.That(notifications[0].Code, Is.EqualTo("reset1"));
        Assert.That(notifications[0].Type, Is.EqualTo(NotificationType.PasswordReset));
        Assert.That(notifications[1].Code, Is.EqualTo("confirm1"));
        Assert.That(notifications[1].Type, Is.EqualTo(NotificationType.EmailConfirmation));
        Assert.That(notifications[2].Code, Is.EqualTo("reset2"));
        Assert.That(notifications[2].Type, Is.EqualTo(NotificationType.PasswordReset));
    }

    [Test]
    public async Task GetNotificationsAsync_MultipleUsers_CapturesEachUserId()
    {
        // Given: Two different users with notifications
        var user1 = new TestUser("alice") { Id = "id-alice" };
        var user2 = new TestUser("bob") { Id = "id-bob" };

        // When: Notifications are sent for both users
        await _notifier.SendResetCodeAsync(user1, "reset-alice");
        await _notifier.SendResetCodeAsync(user2, "reset-bob");

        // Then: Each notification should capture the correct user identity
        var notifications = await _notifier.GetNotificationsAsync();
        Assert.That(notifications, Has.Count.EqualTo(2));
        Assert.That(notifications[0].UserId, Is.EqualTo("id-alice"));
        Assert.That(notifications[0].UserName, Is.EqualTo("alice"));
        Assert.That(notifications[1].UserId, Is.EqualTo("id-bob"));
        Assert.That(notifications[1].UserName, Is.EqualTo("bob"));
    }

    [Test]
    public async Task ClearAsync_RemovesAllNotifications()
    {
        // Given: Notifications have been captured
        var user = new TestUser("testuser") { Id = "user1" };
        await _notifier.SendResetCodeAsync(user, "reset1");
        await _notifier.SendEmailConfirmationAsync(user, "confirm1");

        // When: Clearing all notifications
        await _notifier.ClearAsync();

        // Then: No notifications should remain
        var notifications = await _notifier.GetNotificationsAsync();
        Assert.That(notifications, Is.Empty);
    }
}

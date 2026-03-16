namespace NuxtIdentity.Core.Models;

/// <summary>
/// Types of user notifications captured by the notification system.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// A password reset code notification.
    /// </summary>
    PasswordReset,

    /// <summary>
    /// An email confirmation code notification.
    /// </summary>
    EmailConfirmation
}

/// <summary>
/// Record of a notification sent to a user, captured for testing and diagnostics.
/// </summary>
public record NotificationRecord
{
    /// <summary>
    /// The identity of the user who was notified (e.g. IdentityUser.Id).
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// The username of the user who was notified.
    /// </summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// The code sent to the user (reset code or confirmation code).
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// The type of notification that was sent.
    /// </summary>
    public NotificationType Type { get; init; }

    /// <summary>
    /// The UTC timestamp when the notification was sent.
    /// </summary>
    public DateTime Timestamp { get; init; }
}

namespace NuxtIdentity.Core.Models;

/// <summary>
/// Transport-neutral user identity data required for outbound account notifications.
/// </summary>
/// <remarks>
/// This model intentionally contains only the fields needed by notification
/// implementations and avoids any dependency on host-framework user types.
/// </remarks>
public record NotificationRecipient
{
    /// <summary>
    /// Stable user identifier from the backing identity system.
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Display or login name for personalization.
    /// </summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// Destination email address.
    /// </summary>
    public string Email { get; init; } = string.Empty;
}

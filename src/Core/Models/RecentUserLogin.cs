namespace NuxtIdentity.Core.Models;

/// <summary>
/// Represents a user's most recent login time based on refresh token issuance.
/// </summary>
/// <param name="UserId">The user identifier.</param>
/// <param name="LastLoginAt">The UTC timestamp of the user's most recent login.</param>
public sealed record RecentUserLogin(string UserId, DateTime LastLoginAt);
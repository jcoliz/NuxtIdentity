namespace NuxtIdentity.Core.Models;

/// <summary>
/// Represents a user and their most recent login time based on refresh token issuance.
/// </summary>
/// <typeparam name="TUser">The user type.</typeparam>
/// <param name="User">The user object.</param>
/// <param name="LastLoginAt">The UTC timestamp of the user's most recent login.</param>
public sealed record RecentUserLogin<TUser>(TUser User, DateTime LastLoginAt);
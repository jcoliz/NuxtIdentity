namespace NuxtIdentity.Core.Abstractions;

using NuxtIdentity.Core.Models;

/// <summary>
/// Service for managing refresh tokens.
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>
    /// Generates a new refresh token for the specified user.
    /// </summary>
    /// <param name="userId">The user ID to generate the token for.</param>
    /// <returns>The generated refresh token string.</returns>
    Task<string> GenerateRefreshTokenAsync(string userId);

    /// <summary>
    /// Validates a refresh token and returns the associated user ID if valid.
    /// </summary>
    /// <param name="token">The refresh token to validate.</param>
    /// <returns>The user ID if the token is valid; otherwise, null.</returns>
    /// <remarks>
    /// This method allows token validation without requiring the caller to know the user ID,
    /// which is useful for refresh endpoints that don't require a valid access token.
    /// </remarks>
    Task<string?> ValidateRefreshTokenAsync(string token);

    /// <summary>
    /// Revokes a specific refresh token.
    /// </summary>
    /// <param name="token">The refresh token to revoke.</param>
    Task RevokeRefreshTokenAsync(string token);

    /// <summary>
    /// Revokes all refresh tokens for a specific user.
    /// </summary>
    /// <param name="userId">The user ID whose tokens should be revoked.</param>
    Task RevokeAllUserTokensAsync(string userId);

    /// <summary>
    /// Gets users with refresh token activity ordered by most recent login first.
    /// </summary>
    /// <typeparam name="TUser">The user type.</typeparam>
    /// <param name="users">
    /// Queryable users source used to join with refresh tokens by user ID.
    /// </param>
    /// <param name="userIdPropertyName">
    /// The property name on <typeparamref name="TUser"/> containing the user ID. Defaults to <c>Id</c>.
    /// </param>
    /// <returns>
    /// A sequence containing one item per user with their user object and latest refresh token creation time.
    /// </returns>
    Task<IReadOnlyList<RecentUserLogin<TUser>>> GetUsersLoggedInRecentlyAsync<TUser>(
        IQueryable<TUser> users,
        string userIdPropertyName = "Id") where TUser : class;

}

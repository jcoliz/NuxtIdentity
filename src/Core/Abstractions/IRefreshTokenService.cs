namespace NuxtIdentity.Core.Abstractions;

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
}

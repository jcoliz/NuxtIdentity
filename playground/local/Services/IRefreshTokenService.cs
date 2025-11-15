namespace NuxtIdentity.Playground.Local.Services;

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
    /// Validates a refresh token for the specified user.
    /// </summary>
    /// <param name="token">The refresh token to validate.</param>
    /// <param name="userId">The user ID to validate against.</param>
    /// <returns>True if the token is valid; otherwise, false.</returns>
    Task<bool> ValidateRefreshTokenAsync(string token, string userId);

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

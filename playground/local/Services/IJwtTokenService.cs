using NuxtIdentity.Playground.Local.Models;
using Microsoft.IdentityModel.Tokens;

namespace NuxtIdentity.Playground.Local.Services;

/// <summary>
/// Service for generating and validating JWT tokens.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT access token for the specified user.
    /// </summary>
    /// <param name="user">The user to generate the token for.</param>
    /// <returns>A signed JWT token string.</returns>
    Task<string> GenerateAccessTokenAsync(ApplicationUser user);

    /// <summary>
    /// Validates a JWT token and returns the claims principal.
    /// </summary>
    /// <param name="token">The JWT token to validate.</param>
    /// <returns>The claims principal if valid; otherwise, null.</returns>
    Task<System.Security.Claims.ClaimsPrincipal?> ValidateTokenAsync(string token);

    /// <summary>
    /// Gets the token validation parameters for JWT authentication middleware.
    /// </summary>
    /// <returns>Token validation parameters.</returns>
    TokenValidationParameters GetTokenValidationParameters();
}
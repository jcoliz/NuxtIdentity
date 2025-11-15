using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace NuxtIdentity.Playground.Local.Services;

/// <summary>
/// Service for generating and validating JWT tokens.
/// </summary>
/// <typeparam name="TUser">The type of user this service works with.</typeparam>
/// <remarks>
/// This interface is designed to be generic to support different user types across applications.
/// By using a generic type parameter, the same service implementation can work with any user model,
/// whether it's ASP.NET Core Identity's IdentityUser, a custom user class, or a minimal user DTO.
/// 
/// The service delegates the actual claim extraction to IUserClaimsProvider&lt;TUser&gt;, which
/// allows different applications to customize how user information is converted into JWT claims
/// without modifying the core token generation logic.
/// 
/// This separation enables the core JWT functionality to be packaged in a reusable library while
/// keeping user-model-specific logic in application-specific or technology-specific packages
/// (e.g., NuxtIdentity.Core for the interface, NuxtIdentity.Identity for ASP.NET Identity implementation).
/// </remarks>
public interface IJwtTokenService<TUser> where TUser : class
{
    /// <summary>
    /// Generates a JWT access token for the specified user.
    /// </summary>
    /// <param name="user">The user to generate the token for.</param>
    /// <returns>A signed JWT token string.</returns>
    Task<string> GenerateAccessTokenAsync(TUser user);

    /// <summary>
    /// Validates a JWT token and returns the claims principal.
    /// </summary>
    /// <param name="token">The JWT token to validate.</param>
    /// <returns>The claims principal if valid; otherwise, null.</returns>
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token);

    /// <summary>
    /// Gets the token validation parameters for JWT authentication middleware.
    /// </summary>
    /// <returns>Token validation parameters.</returns>
    /// <remarks>
    /// This method exposes the internal validation parameters to allow the ASP.NET Core
    /// authentication middleware to validate tokens using the same configuration as this service.
    /// This ensures consistency between manual token validation and middleware-based validation.
    /// </remarks>
    TokenValidationParameters GetTokenValidationParameters();
}
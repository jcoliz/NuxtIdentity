using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using NuxtIdentity.Playground.Local.Models;

namespace NuxtIdentity.Playground.Local.Services;

/// <summary>
/// Provides claims for ApplicationUser using ASP.NET Core Identity.
/// </summary>
/// <remarks>
/// This is an ASP.NET Core Identity-specific implementation of IUserClaimsProvider.
/// It demonstrates how to integrate the generic JWT token service with ASP.NET Core Identity
/// by extracting user information and roles from the Identity system.
/// 
/// Design Rationale:
/// 
/// 1. **Technology-Specific Implementation**: This class depends on ASP.NET Core Identity
///    (specifically UserManager&lt;ApplicationUser&gt;), making it suitable for packaging in
///    a separate library like NuxtIdentity.Identity rather than the core library.
/// 
/// 2. **Standard Claims**: Includes common JWT claims that work well with @sidebase/nuxt-auth:
///    - NameIdentifier: User's unique ID from Identity
///    - Name: Username for display and authentication
///    - Email: User's email address
///    - Sub (Subject): Standard JWT claim, typically the username
///    - Jti (JWT ID): Unique identifier for this specific token
///    - Role: User's roles from Identity (can be multiple)
/// 
/// 3. **Async Role Loading**: Uses UserManager.GetRolesAsync to retrieve roles, demonstrating
///    that claim providers can perform async operations to gather user information from
///    various sources (database, cache, external services, etc.).
/// 
/// 4. **Extensibility**: Applications can create their own implementations to add custom claims,
///    integrate with different identity systems, or modify the claim structure without changing
///    the core JWT token generation logic.
/// 
/// Library Packaging:
/// - Belongs in NuxtIdentity.Identity (or similar Identity-specific package)
/// - Depends on Microsoft.AspNetCore.Identity
/// - Provides a ready-to-use implementation for applications using ASP.NET Core Identity
/// </remarks>
public class IdentityUserClaimsProvider : IUserClaimsProvider<ApplicationUser>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityUserClaimsProvider(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Claim>> GetClaimsAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? ""),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Sub, user.UserName ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return claims;
    }
}
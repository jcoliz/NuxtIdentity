using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using NuxtIdentity.Playground.Local.Models;

namespace NuxtIdentity.Playground.Local.Services;

/// <summary>
/// Provides claims for ApplicationUser using ASP.NET Core Identity.
/// </summary>
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
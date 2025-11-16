using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NuxtIdentity.Core.Abstractions;

namespace NuxtIdentity.AspNetCore.Services;

/// <summary>
/// Provides claims for IdentityUser-derived types using ASP.NET Core Identity.
/// </summary>
/// <typeparam name="TUser">The user type, must derive from IdentityUser.</typeparam>
/// <remarks>
/// This is an ASP.NET Core Identity-specific implementation of IUserClaimsProvider.
/// It integrates the generic JWT token service with ASP.NET Core Identity by extracting
/// user information, roles, and claims from the Identity system.
/// 
/// <para><strong>Design Rationale:</strong></para>
/// 
/// <list type="number">
///   <item>
///     <term>Standard Claims</term>
///     <description>
///       Includes common JWT claims that work well with frontend auth libraries:
///       - NameIdentifier: User's unique ID from Identity
///       - Name: Username for display and authentication
///       - Email: User's email address
///       - Sub (Subject): Standard JWT claim, typically the username
///       - Jti (JWT ID): Unique identifier for this specific token
///       - Role: User's roles from Identity (can be multiple)
///     </description>
///   </item>
///   <item>
///     <term>User and Role Claims</term>
///     <description>
///       Includes all claims directly attached to the user via UserManager.AddClaimAsync(),
///       as well as any claims attached to the roles the user belongs to. This provides
///       a complete picture of the user's permissions and attributes. Duplicates are
///       automatically removed, with user claims taking precedence over role claims.
///     </description>
///   </item>
///   <item>
///     <term>Async Role Loading</term>
///     <description>
///       Uses UserManager.GetRolesAsync to retrieve roles, demonstrating that claim providers
///       can perform async operations to gather user information from various sources.
///     </description>
///   </item>
///   <item>
///     <term>Extensibility</term>
///     <description>
///       Applications can create their own implementations to add custom claims, integrate
///       with different identity systems, or modify the claim structure without changing
///       the core JWT token generation logic.
///     </description>
///   </item>
/// </list>
/// </remarks>
public partial class IdentityUserClaimsProvider<TUser> : IUserClaimsProvider<TUser>
    where TUser : IdentityUser
{
    private readonly UserManager<TUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<IdentityUserClaimsProvider<TUser>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityUserClaimsProvider{TUser}"/> class.
    /// </summary>
    /// <param name="userManager">The Identity user manager.</param>
    /// <param name="roleManager">The Identity role manager.</param>
    /// <param name="logger">Logger instance.</param>
    public IdentityUserClaimsProvider(
        UserManager<TUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<IdentityUserClaimsProvider<TUser>> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Claim>> GetClaimsAsync(TUser user)
    {
        LogGeneratingClaims(user.Id);

        var roles = await _userManager.GetRolesAsync(user);
        var userClaims = await _userManager.GetClaimsAsync(user);

        // Use a HashSet to track unique claim type/value pairs
        var claimSet = new HashSet<(string Type, string Value)>();
        var claims = new List<Claim>();

        // Helper to add claim if not duplicate
        void AddClaim(Claim claim)
        {
            if (claimSet.Add((claim.Type, claim.Value)))
            {
                claims.Add(claim);
            }
        }

        // Add standard claims
        AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id));
        AddClaim(new Claim(ClaimTypes.Name, user.UserName ?? ""));
        AddClaim(new Claim(ClaimTypes.Email, user.Email ?? ""));
        AddClaim(new Claim(JwtRegisteredClaimNames.Sub, user.UserName ?? ""));
        AddClaim(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));

        // Add all roles as claims
        foreach (var role in roles)
        {
            AddClaim(new Claim(ClaimTypes.Role, role));
        }
        
        // Add ALL user claims from Identity (these take precedence)
        foreach (var claim in userClaims)
        {
            AddClaim(claim);
        }

        // Add role claims
        var roleClaimCount = 0;
        foreach (var roleName in roles)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                var roleClaims = await _roleManager.GetClaimsAsync(role);
                foreach (var claim in roleClaims)
                {
                    // HashSet ensures no duplicates; user claims added first take precedence
                    var initialCount = claims.Count;
                    AddClaim(claim);
                    if (claims.Count > initialCount)
                    {
                        roleClaimCount++;
                    }
                }
            }
        }

        LogClaimsGenerated(user.Id, claims.Count, roles.Count, userClaims.Count, roleClaimCount);

        return claims;
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generating claims for user: {userId}")]
    private partial void LogGeneratingClaims(string userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generated {claimCount} claims for user: {userId}, including {roleCount} roles, {userClaimCount} user claims, and {roleClaimCount} role claims")]
    private partial void LogClaimsGenerated(string userId, int claimCount, int roleCount, int userClaimCount, int roleClaimCount);

    #endregion
}
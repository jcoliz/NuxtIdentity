using System.IdentityModel.Tokens.Jwt;
using System.Runtime.CompilerServices;
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
        LogStartingUserId(user.Id);

        var roles = await _userManager.GetRolesAsync(user);
        var userClaims = await _userManager.GetClaimsAsync(user);

        var claimBuilder = new ClaimBuilder();

        AddStandardClaims(claimBuilder, user);
        AddRoleClaims(claimBuilder, roles);
        AddUserClaims(claimBuilder, userClaims);
        var roleClaimCount = await AddRoleClaimsAsync(claimBuilder, roles);

        var claims = claimBuilder.GetClaims();
        LogOkUserIdCount(user.Id, claims.Count);

        return claims;
    }

    /// <summary>
    /// Retrieves claims stored in Identity for the user, including user and role claims.
    /// </summary>
    /// <remarks>
    /// Does not include standard JWT claims like sub, jti, etc., as well as claims for
    /// role membership. Does include stored user claims and claims from roles.
    /// </remarks>
    /// <param name="user"></param>
    /// <returns></returns>
    internal async Task<IEnumerable<Claim>> GetStoredClaimsAsync(TUser user)
    {
        LogStartingUserId(user.Id);
    
        var roles = await _userManager.GetRolesAsync(user);
        var userClaims = await _userManager.GetClaimsAsync(user);
        var claimBuilder = new ClaimBuilder();
        AddUserClaims(claimBuilder, userClaims);
        await AddRoleClaimsAsync(claimBuilder, roles);

        var claims = claimBuilder.GetClaims();
        LogOkUserIdCount(user.Id, claims.Count);

        return claims;
    }

    /// <summary>
    /// Adds standard JWT claims for the user.
    /// </summary>
    private static void AddStandardClaims(ClaimBuilder builder, TUser user)
    {
        var _ = builder.AddClaimsWithCount([
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, user.UserName ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        ]);
    }

    /// <summary>
    /// Adds role claims for each role the user belongs to.
    /// </summary>
    private static void AddRoleClaims(ClaimBuilder builder, IList<string> roles)
    {
        foreach (var role in roles)
        {
            builder.AddClaim(new Claim(ClaimTypes.Role, role));
        }
    }

    /// <summary>
    /// Adds all user-specific claims from Identity.
    /// </summary>
    private static void AddUserClaims(ClaimBuilder builder, IList<Claim> userClaims)
    {
        foreach (var claim in userClaims)
        {
            builder.AddClaim(claim);
        }
    }

    /// <summary>
    /// Adds claims from all roles the user belongs to.
    /// </summary>
    /// <returns>The count of role claims added (excluding duplicates).</returns>
    private async Task<int> AddRoleClaimsAsync(ClaimBuilder builder, IList<string> roles)
    {
        var roleClaimCount = 0;

        foreach (var roleName in roles)
        {
            roleClaimCount += await AddClaimsForRoleAsync(builder, roleName);
        }

        return roleClaimCount;
    }

    /// <summary>
    /// Adds claims for a specific role.
    /// </summary>
    /// <returns>The count of claims added for this role (excluding duplicates).</returns>
    private async Task<int> AddClaimsForRoleAsync(ClaimBuilder builder, string roleName)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role == null)
        {
            return 0;
        }

        var roleClaims = await _roleManager.GetClaimsAsync(role);
        return builder.AddClaimsWithCount(roleClaims);
    }

    #region Logger Messages

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting User {UserId}")]
    private partial void LogStartingUserId(string userId, [CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK User {UserId} {Count} results")]
    private partial void LogOkUserIdCount(string userId, int count, [CallerMemberName] string? location = null);

    #endregion

    /// <summary>
    /// Helper class to build a deduplicated collection of claims.
    /// </summary>
    private class ClaimBuilder
    {
        private readonly HashSet<(string Type, string Value)> _claimSet = new();
        private readonly List<Claim> _claims = new();

        /// <summary>
        /// Adds a claim if it's not a duplicate.
        /// </summary>
        /// <returns>True if the claim was added, false if it was a duplicate.</returns>
        public bool AddClaim(Claim claim)
        {
            if (_claimSet.Add((claim.Type, claim.Value)))
            {
                _claims.Add(claim);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Adds multiple claims and returns the count of claims actually added (excluding duplicates).
        /// </summary>
        public int AddClaimsWithCount(IEnumerable<Claim> claims)
        {
            var count = 0;
            foreach (var claim in claims)
            {
                if (AddClaim(claim))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Gets the final list of claims.
        /// </summary>
        public List<Claim> GetClaims() => _claims;
    }
}

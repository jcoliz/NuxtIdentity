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
/// user information and roles from the Identity system.
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
    private readonly ILogger<IdentityUserClaimsProvider<TUser>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityUserClaimsProvider{TUser}"/> class.
    /// </summary>
    /// <param name="userManager">The Identity user manager.</param>
    /// <param name="logger">Logger instance.</param>
    public IdentityUserClaimsProvider(
        UserManager<TUser> userManager,
        ILogger<IdentityUserClaimsProvider<TUser>> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Claim>> GetClaimsAsync(TUser user)
    {
        LogGeneratingClaims(user.Id);

        var roles = await _userManager.GetRolesAsync(user);
        var userClaims = await _userManager.GetClaimsAsync(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? ""),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Sub, user.UserName ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Add all roles as claims
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        
        // Add ALL user claims from Identity
        // This includes any custom claims added via UserManager.AddClaimAsync()
        claims.AddRange(userClaims);

        LogClaimsGenerated(user.Id, claims.Count, roles.Count, userClaims.Count);

        return claims;
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generating claims for user: {userId}")]
    private partial void LogGeneratingClaims(string userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generated {claimCount} claims for user: {userId}, including {roleCount} roles and {userClaimCount} user claims")]
    private partial void LogClaimsGenerated(string userId, int claimCount, int roleCount, int userClaimCount);

    #endregion
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using NuxtIdentity.AspNetCore.Controllers;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;
using NuxtIdentity.Playground.Local.Models;

namespace NuxtIdentity.Playground.Local.Controllers;

/// <summary>
/// Authentication controller that demonstrates how to use NuxtAuthControllerBase
/// with ASP.NET Core Identity.
/// </summary>
/// <remarks>
/// <para><strong>Design Rationale:</strong></para>
/// <para>
/// This controller inherits from <see cref="NuxtAuthControllerBase{TUser}"/> to gain
/// common JWT authentication infrastructure while implementing application-specific
/// authentication logic using ASP.NET Core Identity.
/// </para>
/// 
/// <para><strong>What the Base Class Provides:</strong></para>
/// <list type="bullet">
///   <item><description>Virtual RefreshTokens endpoint - handles token refresh with rotation</description></item>
///   <item><description>Virtual Logout endpoint - revokes refresh tokens</description></item>
///   <item><description>Helper methods for creating token responses</description></item>
///   <item><description>Helper methods for extracting user claims</description></item>
///   <item><description>Structured logging infrastructure</description></item>
///   <item><description>Protected access to IJwtTokenService and IRefreshTokenService</description></item>
/// </list>
/// 
/// <para><strong>What This Class Implements:</strong></para>
/// <list type="bullet">
///   <item><description>GetUserByIdAsync - required abstract method for user lookup</description></item>
///   <item><description>Login endpoint - Identity-specific authentication logic</description></item>
///   <item><description>GetSession endpoint - retrieves current user information</description></item>
/// </list>
/// 
/// <para><strong>Why This Design:</strong></para>
/// <para>
/// The base class is deliberately generic and doesn't depend on ASP.NET Core Identity,
/// making it reusable across different authentication systems (Identity, custom repositories,
/// OAuth providers, etc.). This controller demonstrates the Identity-specific implementation
/// pattern, but the same base class could be used with any user management system.
/// </para>
/// 
/// <para>
/// Login and signup logic intentionally live in derived classes because they vary
/// significantly across applications - some use username/password, others use email,
/// some have social logins, some require email verification, etc. The base class
/// handles the common concerns (token generation, refresh, logout) that work the
/// same way regardless of how users authenticate.
/// </para>
/// </remarks>
public partial class AuthController(
    IJwtTokenService<ApplicationUser> jwtTokenService,
    IRefreshTokenService refreshTokenService,
    UserManager<ApplicationUser> userManager,      // Identity-specific
    SignInManager<ApplicationUser> signInManager,  // Identity-specific
    ILogger<AuthController> logger) 
    : NuxtAuthControllerBase<ApplicationUser>(jwtTokenService, refreshTokenService, logger)
{
    // Implement the abstract method
    protected override async Task<ApplicationUser?> GetUserByIdAsync(string userId)
    {
        return await userManager.FindByIdAsync(userId);
    }

    // Implement application-specific endpoints
    /// <summary>
    /// Authenticates a user with username and password.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <returns>JWT tokens and user information if successful; otherwise, unauthorized.</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByNameAsync(request.Username);
        if (user == null)
        {
            return Unauthorized(new { message = "Invalid credentials" });
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            var userInfo = new UserInfo
            {
                Id = user.Id,
                Name = user.DisplayName ?? user.UserName ?? "",
                Email = user.Email ?? "",
                Role = "admin"
            };

            var response = await CreateLoginResponseAsync(user, user.Id, userInfo);
            return Ok(response);
        }

        return Unauthorized(new { message = "Invalid credentials" });
    }

    /// <summary>
    /// Retrieves the current user's session information.
    /// </summary>
    /// <returns>User information if authenticated; otherwise, unauthorized.</returns>
    /// <remarks>Requires a valid JWT access token in the Authorization header.</remarks>
    [HttpGet("user")]
    [Authorize]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSession()
    {
        var username = GetCurrentUsername();
        if (username == null)
        {
            return Unauthorized();
        }

        var user = await userManager.FindByNameAsync(username);
        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(new SessionResponse
        {
            User = new UserInfo
            {
                Id = user.Id,
                Name = user.DisplayName ?? user.UserName ?? "",
                Email = user.Email ?? "",
                Role = "account"
            }
        });
    }

    // Refresh and Logout are inherited from base class
    // but can be overridden if needed
}

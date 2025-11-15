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
///   <item><description>SignUp endpoint - User registration with 'guest' role assignment</description></item>
///   <item><description>GetSession endpoint - retrieves current user information</description></item>
///   <item><description>SetRole endpoint (admin only) - changes user roles</description></item>
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

    /// <summary>
    /// Registers a new user with 'guest' role.
    /// </summary>
    /// <param name="request">Signup credentials.</param>
    /// <returns>JWT tokens and user information if successful; otherwise, bad request.</returns>
    [HttpPost("signup")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
    {
        LogSignupAttempt(request.Username);
        
        var user = new ApplicationUser
        {
            UserName = request.Username,
            Email = $"{request.Username}@sample.com",
            DisplayName = request.Username
        };
        
        var result = await userManager.CreateAsync(user, request.Password);
        
        if (!result.Succeeded)
        {
            LogSignupFailed(request.Username, string.Join(", ", result.Errors.Select(e => e.Description)));
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }
        
        // Assign 'guest' role to new users
        await userManager.AddToRoleAsync(user, "guest");
        LogSignupSuccess(request.Username);
        
        var userInfo = new UserInfo
        {
            Id = user.Id,
            Name = user.DisplayName,
            Email = user.Email ?? "",
            Role = "guest"
        };
        
        var response = await CreateLoginResponseAsync(user, user.Id, userInfo);
        return Ok(response);
    }

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
        LogLoginAttempt(request.Username);
        
        var user = await userManager.FindByNameAsync(request.Username);
        if (user == null)
        {
            LogLoginFailed(request.Username, "User not found");
            return Unauthorized(new { message = "Invalid credentials" });
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            var roles = await userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "guest";
            
            var userInfo = new UserInfo
            {
                Id = user.Id,
                Name = user.DisplayName,
                Email = user.Email ?? "",
                Role = role
            };

            var response = await CreateLoginResponseAsync(user, user.Id, userInfo);
            LogLoginSuccess(request.Username, role);
            return Ok(response);
        }

        LogLoginFailed(request.Username, "Invalid password");
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

        var roles = await userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "guest";

        return Ok(new SessionResponse
        {
            User = new UserInfo
            {
                Id = user.Id,
                Name = user.DisplayName,
                Email = user.Email ?? "",
                Role = role
            }
        });
    }

    /// <summary>
    /// Changes a user's role (admin only).
    /// </summary>
    /// <param name="request">User ID and new role.</param>
    /// <returns>Success if role changed; otherwise, error.</returns>
    [HttpPost("setrole")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetRole([FromBody] SetRoleRequest request)
    {
        LogSetRoleAttempt(request.UserId, request.Role);
        
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            LogSetRoleFailed(request.UserId, "User not found");
            return NotFound(new { message = "User not found" });
        }
        
        var validRoles = new[] { "guest", "account", "admin" };
        if (!validRoles.Contains(request.Role))
        {
            LogSetRoleFailed(request.UserId, "Invalid role");
            return BadRequest(new { message = "Invalid role. Must be 'guest', 'account', or 'admin'" });
        }
        
        // Remove all current roles
        var currentRoles = await userManager.GetRolesAsync(user);
        await userManager.RemoveFromRolesAsync(user, currentRoles);
        
        // Add new role
        var result = await userManager.AddToRoleAsync(user, request.Role);
        
        if (!result.Succeeded)
        {
            LogSetRoleFailed(request.UserId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }
        
        LogSetRoleSuccess(request.UserId, request.Role);
        return Ok(new { success = true, userId = request.UserId, role = request.Role });
    }

    // Refresh and Logout are inherited from base class
    // but can be overridden if needed

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Signup attempt for user: {username}")]
    private partial void LogSignupAttempt(string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Signup successful for user: {username}")]
    private partial void LogSignupSuccess(string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Signup failed for user: {username}. Errors: {errors}")]
    private partial void LogSignupFailed(string username, string errors);

    [LoggerMessage(Level = LogLevel.Information, Message = "Login attempt for user: {username}")]
    private partial void LogLoginAttempt(string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Login successful for user: {username}, role: {role}")]
    private partial void LogLoginSuccess(string username, string role);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login failed for user: {username}. Reason: {reason}")]
    private partial void LogLoginFailed(string username, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Set role attempt for user: {userId} to role: {role}")]
    private partial void LogSetRoleAttempt(string userId, string role);

    [LoggerMessage(Level = LogLevel.Information, Message = "Set role successful for user: {userId} to role: {role}")]
    private partial void LogSetRoleSuccess(string userId, string role);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Set role failed for user: {userId}. Reason: {reason}")]
    private partial void LogSetRoleFailed(string userId, string reason);

    #endregion
}

/// <summary>
/// Request model for setting a user's role.
/// </summary>
public record SetRoleRequest
{
    /// <summary>
    /// The ID of the user whose role should be changed.
    /// </summary>
    public required string UserId { get; init; }
    
    /// <summary>
    /// The new role to assign ('guest', 'account', or 'admin').
    /// </summary>
    public required string Role { get; init; }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using NuxtIdentity.AspNetCore.Controllers;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;
using NuxtIdentity.Playground.Local.Models;

namespace NuxtIdentity.Playground.Local.Controllers;

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

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using NuxtIdentity.Playground.Local.Models;
using NuxtIdentity.Playground.Local.Services;

namespace NuxtIdentity.Playground.Local.Controllers;

/// <summary>
/// Handles authentication operations including login, signup, token refresh, and session management.
/// </summary>
/// <param name="jwtTokenService">JWT token service.</param>
/// <param name="refreshTokenService">Refresh token service.</param>
/// <param name="userManager">User manager for Identity.</param>
/// <param name="signInManager">Sign in manager for Identity.</param>
/// <param name="logger">Logger instance.</param>
[ApiController]
[Route("api/auth")]
public partial class AuthController(
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<AuthController> logger) : ControllerBase
{
    #region Public Endpoints

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
            LogLoginFailed(request.Username);
            return Unauthorized(new { message = "Invalid credentials" });
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            var token = await jwtTokenService.GenerateAccessTokenAsync(user);
            var refreshToken = await refreshTokenService.GenerateRefreshTokenAsync(user.Id);
            
            LogLoginSuccess(request.Username);
            return Ok(new LoginResponse
            {
                Token = new TokenPair
                {
                    AccessToken = token,
                    RefreshToken = refreshToken
                },
                User = new UserInfo
                {
                    Id = user.Id,
                    Name = user.DisplayName ?? user.UserName ?? "",
                    Email = user.Email ?? "",
                    Role = "admin"
                }
            });
        }

        LogLoginFailed(request.Username);
        return Unauthorized(new { message = "Invalid credentials" });
    }

    /// <summary>
    /// Registers a new user account.
    /// </summary>
    /// <param name="request">Signup credentials.</param>
    /// <returns>JWT tokens and user information for the newly created account.</returns>
    [HttpPost("signup")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
    {
        LogSignUpAttempt(request.Username);

        var user = new ApplicationUser
        {
            UserName = request.Username,
            Email = request.Email ?? $"{request.Username}@example.com",
            DisplayName = request.Username
        };

        var result = await userManager.CreateAsync(user, request.Password);
        
        if (result.Succeeded)
        {
            var token = await jwtTokenService.GenerateAccessTokenAsync(user);
            var refreshToken = await refreshTokenService.GenerateRefreshTokenAsync(user.Id);
            
            LogSignupSuccess(request.Username);
            return Ok(new LoginResponse
            {
                Token = new TokenPair
                {
                    AccessToken = token,
                    RefreshToken = refreshToken
                },
                User = new UserInfo
                {
                    Id = user.Id,
                    Name = user.DisplayName,
                    Email = user.Email,
                    Role = "guest"
                }
            });
        }

        return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
    }

    /// <summary>
    /// Refreshes an expired access token using a valid refresh token.
    /// </summary>
    /// <param name="request">Refresh token.</param>
    /// <returns>New JWT token pair if successful; otherwise, unauthorized.</returns>
    /// <remarks>Requires a valid JWT access token in the Authorization header.</remarks>
    [HttpPost("refresh")]
    [Authorize]
    [ProducesResponseType(typeof(RefreshResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshTokens([FromBody] RefreshRequest request)
    {
        LogRefreshAttempt(request.RefreshToken);

        var username = User.Identity?.Name;
        if (username == null)
        {
            LogRefreshNoToken();
            return Unauthorized();
        }

        var user = await userManager.FindByNameAsync(username);
        if (user == null)
        {
            return Unauthorized();
        }
        
        // Validate the refresh token
        var isValid = await refreshTokenService.ValidateRefreshTokenAsync(request.RefreshToken, user.Id);
        if (isValid)
        {
            // Revoke old token and issue new ones (token rotation)
            await refreshTokenService.RevokeRefreshTokenAsync(request.RefreshToken);
            
            var newAccessToken = await jwtTokenService.GenerateAccessTokenAsync(user);
            var newRefreshToken = await refreshTokenService.GenerateRefreshTokenAsync(user.Id);
            
            LogRefreshSuccess(username, newRefreshToken);
            return Ok(new RefreshResponse()
            { 
                Token = new TokenPair
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken
                }
            });
        }

        LogRefreshInvalidToken(username);
        return Unauthorized(new { message = "Invalid refresh token" });
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
        LogSessionValidationStarted();

        var username = User.Identity?.Name;
        if (username == null)
        {
            LogSessionValidationNoToken();
            return Unauthorized();
        }

        var user = await userManager.FindByNameAsync(username);
        if (user == null)
        {
            return Unauthorized();
        }

        LogSessionValidationSuccess(username);
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

    /// <summary>
    /// Logs out the current user.
    /// </summary>
    /// <returns>Success response.</returns>
    /// <remarks>In a stateless JWT setup, logout is handled client-side by discarding tokens.</remarks>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        LogLogoutRequested();
        
        // Revoke the refresh token
        if (!string.IsNullOrEmpty(request.RefreshToken))
        {
            await refreshTokenService.RevokeRefreshTokenAsync(request.RefreshToken);
        }
        
        return Ok(new { success = true });
    }

    #endregion

    #region Logger Messages

    // Login
    [LoggerMessage(Level = LogLevel.Information, Message = "Login attempt for user: {username}")]
    private partial void LogLoginAttempt(string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Login successful for user: {username}")]
    private partial void LogLoginSuccess(string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login failed for user: {username}")]
    private partial void LogLoginFailed(string username);

    // Signup
    [LoggerMessage(Level = LogLevel.Information, Message = "Signup attempt for user: {username}")]
    private partial void LogSignUpAttempt(string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Signup successful for user: {username}")]
    private partial void LogSignupSuccess(string username);

    // Session Validation
    [LoggerMessage(Level = LogLevel.Debug, Message = "Session validation started")]
    private partial void LogSessionValidationStarted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Session validation: no token provided")]
    private partial void LogSessionValidationNoToken();

    [LoggerMessage(Level = LogLevel.Information, Message = "Session validation successful for user: {username}")]
    private partial void LogSessionValidationSuccess(string username);

    // Refresh Token
    [LoggerMessage(Level = LogLevel.Information, Message = "Refresh token attempt started. Token: {RefreshToken}")]
    private partial void LogRefreshAttempt(string refreshToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token attempt: no token provided")]
    private partial void LogRefreshNoToken();

    [LoggerMessage(Level = LogLevel.Information, Message = "Refresh token successful for user: {username}. New token: {RefreshToken}")]
    private partial void LogRefreshSuccess(string username, string refreshToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token invalid for user: {username}")]
    private partial void LogRefreshInvalidToken(string username);

    // Logout
    [LoggerMessage(Level = LogLevel.Information, Message = "Logout requested")]
    private partial void LogLogoutRequested();

    #endregion
}

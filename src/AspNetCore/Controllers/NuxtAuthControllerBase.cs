using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.AspNetCore.Controllers;

/// <summary>
/// Base controller for NuxtIdentity authentication endpoints.
/// </summary>
/// <typeparam name="TUser">The type of user this controller works with.</typeparam>
/// <remarks>
/// This base controller provides common infrastructure for authentication endpoints
/// without depending on any specific user store (ASP.NET Identity, custom, etc.).
/// 
/// It's designed to be extended by application-specific controllers that implement
/// the actual authentication logic using their chosen user management system.
/// 
/// The controller provides:
/// - Protected access to JWT token service
/// - Protected access to refresh token service
/// - Structured logging support
/// - Common response models
/// - Standard endpoint routing
/// 
/// Applications should inherit from this class and implement their own authentication
/// logic (login, signup, etc.) using UserManager, custom repositories, or other
/// user management approaches.
/// </remarks>
[ApiController]
[Route("api/auth")]
public abstract partial class NuxtAuthControllerBase<TUser>(
    IJwtTokenService<TUser> jwtTokenService,
    IRefreshTokenService refreshTokenService,
    ILogger logger) : ControllerBase 
    where TUser : class
{
    /// <summary>
    /// Gets the JWT token service for generating and validating tokens.
    /// </summary>
    protected IJwtTokenService<TUser> JwtTokenService { get; } = jwtTokenService;
    
    /// <summary>
    /// Gets the refresh token service for managing refresh tokens.
    /// </summary>
    protected IRefreshTokenService RefreshTokenService { get; } = refreshTokenService;
    
    #region Helper Methods

    /// <summary>
    /// Creates a login response with tokens and user information.
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="userInfo">User information for the response.</param>
    /// <returns>A login response containing tokens and user data.</returns>
    protected async Task<LoginResponse> CreateLoginResponseAsync(
        TUser user,
        string userId,
        UserInfo userInfo)
    {
        var accessToken = await JwtTokenService.GenerateAccessTokenAsync(user);
        var refreshToken = await RefreshTokenService.GenerateRefreshTokenAsync(userId);

        return new LoginResponse
        {
            Token = new TokenPair
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            },
            User = userInfo
        };
    }

    /// <summary>
    /// Creates a refresh response with new tokens.
    /// </summary>
    /// <param name="user">The user to generate tokens for.</param>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="oldRefreshToken">The old refresh token to revoke.</param>
    /// <returns>A refresh response containing new token pair.</returns>
    protected async Task<RefreshResponse> CreateRefreshResponseAsync(
        TUser user,
        string userId,
        string oldRefreshToken)
    {
        // Revoke old token (token rotation)
        await RefreshTokenService.RevokeRefreshTokenAsync(oldRefreshToken);

        var newAccessToken = await JwtTokenService.GenerateAccessTokenAsync(user);
        var newRefreshToken = await RefreshTokenService.GenerateRefreshTokenAsync(userId);

        return new RefreshResponse
        {
            Token = new TokenPair
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            }
        };
    }

    /// <summary>
    /// Gets the current user's ID from the claims principal.
    /// </summary>
    /// <returns>The user ID if authenticated; otherwise, null.</returns>
    protected string? GetCurrentUserId()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    /// Gets the current user's name from the claims principal.
    /// </summary>
    /// <returns>The username if authenticated; otherwise, null.</returns>
    protected string? GetCurrentUsername()
    {
        return User.Identity?.Name;
    }

    #endregion

    #region Abstract Methods - Must Be Implemented

    // Note: We don't define abstract methods for Login/Signup here because
    // the implementation will vary greatly depending on the authentication system
    // (Identity vs custom) and the application's requirements.
    
    // Applications should implement their own:
    // - [HttpPost("login")] Login(LoginRequest request)
    // - [HttpPost("signup")] SignUp(SignUpRequest request)
    // - [HttpGet("user")] GetSession()
    // etc.

    #endregion

    #region Optional Virtual Methods - Can Be Overridden

    /// <summary>
    /// Handles token refresh logic. Can be overridden for custom behavior.
    /// </summary>
    /// <param name="request">The refresh token request.</param>
    /// <returns>New token pair if successful; otherwise, unauthorized.</returns>
    [HttpPost("refresh")]
    [Authorize]
    [ProducesResponseType(typeof(RefreshResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public virtual async Task<IActionResult> RefreshTokens([FromBody] RefreshRequest request)
    {
        LogRefreshAttempt(request.RefreshToken);

        var userId = GetCurrentUserId();
        var username = GetCurrentUsername();
        
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
        {
            LogRefreshNoToken();
            return Unauthorized();
        }
        
        // Validate the refresh token
        var isValid = await RefreshTokenService.ValidateRefreshTokenAsync(request.RefreshToken, userId);
        if (!isValid)
        {
            LogRefreshInvalidToken(username);
            return Unauthorized(new { message = "Invalid refresh token" });
        }

        // Get the user - this must be implemented by derived class
        var user = await GetUserByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized();
        }

        var response = await CreateRefreshResponseAsync(user, userId, request.RefreshToken);
        LogRefreshSuccess(username, response.Token.RefreshToken);
        
        return Ok(response);
    }

    /// <summary>
    /// Handles logout logic. Can be overridden for custom behavior.
    /// </summary>
    /// <param name="request">The logout request containing the refresh token to revoke.</param>
    /// <returns>Success response.</returns>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public virtual async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        LogLogoutRequested();
        
        if (!string.IsNullOrEmpty(request.RefreshToken))
        {
            await RefreshTokenService.RevokeRefreshTokenAsync(request.RefreshToken);
        }
        
        return Ok(new { success = true });
    }

    /// <summary>
    /// Gets a user by their ID. Must be implemented by derived classes.
    /// </summary>
    /// <param name="userId">The user ID to look up.</param>
    /// <returns>The user if found; otherwise, null.</returns>
    protected abstract Task<TUser?> GetUserByIdAsync(string userId);

    #endregion

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Refresh token attempt started. Token: {RefreshToken}")]
    private partial void LogRefreshAttempt(string refreshToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token attempt: no token provided")]
    private partial void LogRefreshNoToken();

    [LoggerMessage(Level = LogLevel.Information, Message = "Refresh token successful for user: {username}. New token: {RefreshToken}")]
    private partial void LogRefreshSuccess(string username, string refreshToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token invalid for user: {username}")]
    private partial void LogRefreshInvalidToken(string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Logout requested")]
    private partial void LogLogoutRequested();

    #endregion
}
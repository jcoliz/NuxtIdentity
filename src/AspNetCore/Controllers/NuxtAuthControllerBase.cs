using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Exceptions;
using NuxtIdentity.Core.Models;
using System.IdentityModel.Tokens.Jwt;

namespace NuxtIdentity.AspNetCore.Controllers;

/// <summary>
/// Base controller for NuxtIdentity authentication endpoints with ASP.NET Core Identity integration.
/// </summary>
/// <typeparam name="TUser">The type of user this controller works with. Must inherit from IdentityUser.</typeparam>
/// <remarks>
/// <para>
/// This base controller provides complete authentication endpoints that work with ASP.NET Core Identity:
/// - Login: Username/password authentication
/// - SignUp: User registration
/// - Session: Get current user information
/// - Refresh: Token refresh with rotation
/// - Logout: Token revocation
/// </para>
///
/// <para><strong>Default Behavior:</strong></para>
/// <para>
/// All endpoints have sensible default implementations that work with standard IdentityUser.
/// The defaults handle:
/// - User authentication via SignInManager
/// - User creation via UserManager
/// - Role and claim extraction from Identity
/// - Token generation and validation
/// </para>
///
/// <para><strong>Customization:</strong></para>
/// <para>
/// All endpoint methods are virtual and can be overridden for custom behavior.
/// Common scenarios for overriding:
/// - Custom user properties (extend IdentityUser)
/// - Additional validation logic
/// - Email verification requirements
/// - Multi-factor authentication
/// - Custom response formats
/// </para>
///
/// <para><strong>User Information Mapping:</strong></para>
/// <para>
/// The controller automatically maps ASP.NET Core Identity data to the UserInfo model:
/// - Id: User.Id
/// - Name: User.UserName
/// - Email: User.Email
/// - Roles: All roles assigned to the user
/// - Claims: All user claims and role claims
/// </para>
/// </remarks>
[ApiController]
[Route("api/auth")]
public abstract partial class NuxtAuthControllerBase<TUser>(
    IJwtTokenService<TUser> jwtTokenService,
    IEnumerable<IUserClaimsProvider<TUser>> claimsProviders,
    IRefreshTokenService refreshTokenService,
    UserManager<TUser> userManager,
    SignInManager<TUser> signInManager,
    IEnumerable<IUserNotifier<TUser>> userNotifiers,
    ILogger logger) : ControllerBase
    where TUser : IdentityUser, new()
{
    /// <summary>
    /// Gets the JWT token service for generating and validating tokens.
    /// </summary>
    protected IJwtTokenService<TUser> JwtTokenService { get; } = jwtTokenService;

    /// <summary>
    /// Gets the refresh token service for managing refresh tokens.
    /// </summary>
    protected IRefreshTokenService RefreshTokenService { get; } = refreshTokenService;

    /// <summary>
    /// Gets the user manager for Identity operations.
    /// </summary>
    protected UserManager<TUser> UserManager { get; } = userManager;

    /// <summary>
    /// Gets the sign-in manager for authentication operations.
    /// </summary>
    protected SignInManager<TUser> SignInManager { get; } = signInManager;

    /// <summary>
    /// Gets the user notifier for sending notifications, or null if none is registered.
    /// </summary>
    protected IUserNotifier<TUser>? UserNotifier { get; } = userNotifiers.FirstOrDefault();

    #region Helper Methods

    /// <summary>
    /// Creates a login response with tokens and user information.
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <returns>A login response containing tokens and user data.</returns>
    protected async Task<LoginResponse> CreateLoginResponseAsync(TUser user)
    {
        var accessToken = await JwtTokenService.GenerateAccessTokenAsync(user);
        var refreshToken = await RefreshTokenService.GenerateRefreshTokenAsync(user.Id);
        var userInfo = await CreateUserInfoAsync(user);

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
    /// <param name="oldRefreshToken">The old refresh token to revoke.</param>
    /// <returns>A refresh response containing new token pair.</returns>
    protected async Task<RefreshResponse> CreateRefreshResponseAsync(
        TUser user,
        string oldRefreshToken)
    {
        LogStartingUsername(user.UserName ?? "unknown");

        // Revoke old token (token rotation)
        await RefreshTokenService.RevokeRefreshTokenAsync(oldRefreshToken);

        var newAccessToken = await JwtTokenService.GenerateAccessTokenAsync(user);
        var newRefreshToken = await RefreshTokenService.GenerateRefreshTokenAsync(user.Id);

        LogOkUsername(user.UserName ?? "unknown");

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
    /// Creates a UserInfo object from an IdentityUser with roles and claims.
    /// </summary>
    /// <param name="user">The user to create info for.</param>
    /// <returns>UserInfo populated with user data, roles, and claims.</returns>
    protected virtual async Task<UserInfo> CreateUserInfoAsync(TUser user)
    {
        var roles = await UserManager.GetRolesAsync(user);
        var userClaims = await UserManager.GetClaimsAsync(user);

        var providedClaims = new List<Claim>();
        foreach (var provider in claimsProviders)
        {
            if (provider is Services.IdentityUserClaimsProvider<TUser> identityProvider)
            {
                // Only get the stored claims, not the identity claims
                var identityClaims = await identityProvider.GetStoredClaimsAsync(user);
                providedClaims.AddRange(identityClaims);
            }
            else
            {
                var providerClaims = await provider.GetClaimsAsync(user);
                providedClaims.AddRange(providerClaims);                                
            }
        }

        var allClaims = userClaims
            .Concat(providedClaims)
            .Select(c => new ClaimInfo { Type = c.Type, Value = c.Value })
            .Distinct()
            .ToArray();

        return new UserInfo
        {
            Id = user.Id,
            Name = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Roles = roles.ToArray(),
            Claims = allClaims
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

    /// <summary>
    /// Gets a user by their ID. Can be overridden for custom user lookup logic.
    /// </summary>
    /// <param name="userId">The user ID to look up.</param>
    /// <returns>The user if found; otherwise, null.</returns>
    protected virtual async Task<TUser?> GetUserByIdAsync(string userId)
    {
        return await UserManager.FindByIdAsync(userId);
    }

    /// <summary>
    /// Finds a user by username or email address.
    /// </summary>
    /// <param name="username">The username to search for, or null.</param>
    /// <param name="email">The email address to search for, or null.</param>
    /// <returns>The user if found; otherwise, null.</returns>
    private async Task<TUser?> FindUserByUsernameOrEmailAsync(string? username, string? email)
    {
        if (!string.IsNullOrEmpty(username))
            return await UserManager.FindByNameAsync(username);

        if (!string.IsNullOrEmpty(email))
            return await UserManager.FindByEmailAsync(email);

        return null;
    }

    #endregion

    #region Authentication Endpoints

    /// <summary>
    /// Authenticates a user with username and password.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <returns>JWT tokens and user information if successful; otherwise, unauthorized.</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public virtual async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        LogStartingUsername(request.Username);

        var user = await UserManager.FindByNameAsync(request.Username);
        if (user == null)
        {
            LogLoginFailedUsername(request.Username, "User not found");
            return Problem(
                title: "Authentication Failed",
                detail: "Invalid credentials",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var result = await SignInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            LogLoginFailedUsername(request.Username, "Invalid password");
            return Problem(
                title: "Authentication Failed",
                detail: "Invalid credentials",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var response = await CreateLoginResponseAsync(user);
        LogOkUsername(request.Username);
        return Ok(response);
    }

    /// <summary>
    /// Registers a new user.
    /// </summary>
    /// <param name="request">Signup credentials.</param>
    /// <returns>JWT tokens and user information if successful; otherwise, bad request.</returns>
    [HttpPost("signup")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public virtual async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
    {
        LogStartingUsername(request.Username);

        var user = new TUser
        {
            UserName = request.Username,
            Email = request.Email
        };

        var result = await UserManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            LogSignupFailedUsername(request.Username, string.Join(", ", result.Errors.Select(e => e.Description)));

            return Problem(
                title: "Registration Failed",
                detail: string.Join("; ", result.Errors.Select(e => e.Description)),
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        await OnUserCreatedAsync(user);

        var response = await CreateLoginResponseAsync(user);
        LogOkUsername(request.Username);
        return Ok(response);
    }

    /// <summary>
    /// Retrieves the current user's session information.
    /// </summary>
    /// <returns>User information if authenticated; otherwise, unauthorized.</returns>
    /// <remarks>Requires a valid JWT access token in the Authorization header.</remarks>
    [HttpGet("user")]
    [Authorize]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public virtual async Task<IActionResult> GetSession()
    {
        LogStarting();

        var username = GetCurrentUsername();
        if (username == null)
        {
            LogSessionUnauthorized("No username in token");
            return Problem(
                title: "Authentication Required",
                detail: "No valid authentication token provided",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var user = await UserManager.FindByNameAsync(username);
        if (user == null)
        {
            LogSessionUnauthorizedUsername($"User not found: {username}");
            return Problem(
                title: "User Not Found",
                detail: "The authenticated user no longer exists",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var userInfo = await CreateUserInfoAsync(user);
        LogOkUsername(username);

        return Ok(new SessionResponse
        {
            User = userInfo
        });
    }

    /// <summary>
    /// Handles token refresh logic. Can be overridden for custom behavior.
    /// </summary>
    /// <param name="request">The refresh token request.</param>
    /// <returns>New token pair if successful; otherwise, unauthorized.</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public virtual async Task<IActionResult> RefreshTokens([FromBody] RefreshRequest request)
    {
        LogStarting();

        // Validate the refresh token and get user ID (no JWT required)
        var userId = await RefreshTokenService.ValidateRefreshTokenAsync(request.RefreshToken);
        if (string.IsNullOrEmpty(userId))
        {
            LogRefreshTokenInvalid();
            return Problem(
                title: "Token Refresh Failed",
                detail: "Invalid or expired refresh token",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        // Get the user
        var user = await GetUserByIdAsync(userId);
        if (user == null)
        {
            LogRefreshTokenNoUser(userId);
            return Problem(
                title: "User Not Found",
                detail: "The authenticated user no longer exists",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var response = await CreateRefreshResponseAsync(user, request.RefreshToken);

        LogOkUsername(user.UserName ?? "unknown");

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
        LogStarting();

        if (!string.IsNullOrEmpty(request.RefreshToken))
        {
            await RefreshTokenService.RevokeRefreshTokenAsync(request.RefreshToken);
        }

        LogOk();
        return Ok(new { success = true });
    }

    #endregion

    #region Password Management Endpoints

    /// <summary>
    /// Initiates a password reset flow by generating a reset code and notifying the user.
    /// </summary>
    /// <param name="request">The forgot password request containing a username or email.</param>
    /// <remarks>
    /// Always returns 200 OK regardless of whether the user exists to prevent user enumeration.
    /// </remarks>
    /// <exception cref="NuxtIdentityConfigurationException">
    /// Thrown when no <see cref="IUserNotifier{TUser}"/> implementation is registered.
    /// The consumer must register an implementation in DI for the forgot-password flow to work.
    /// </exception>
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public virtual async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        LogStarting();

        if (UserNotifier == null)
        {
            LogNoUserNotifierConfigured();
            throw new NuxtIdentityConfigurationException(
                nameof(IUserNotifier<TUser>));
        }

        var user = await FindUserByUsernameOrEmailAsync(request.Username, request.Email);

        if (user != null)
        {
            LogFoundUser(user.Id);

            var code = await UserManager.GeneratePasswordResetTokenAsync(user);
            await UserNotifier.SendResetCodeAsync(user, code);
        }

        // Always return success to prevent user enumeration
        LogOk();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Resets a user's password using a reset code from the forgot-password flow.
    /// </summary>
    /// <param name="request">The reset password request containing user identifier, reset code, and new password.</param>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public virtual async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        LogStarting();

        var user = await FindUserByUsernameOrEmailAsync(request.Username, request.Email);

        if (user == null)
        {
            LogResetPasswordFailed("User not found");
            return Problem(
                title: "Password Reset Failed",
                detail: "Invalid request",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        LogFoundUser(user.Id);

        var result = await UserManager.ResetPasswordAsync(user, request.Code, request.NewPassword);

        if (!result.Succeeded)
        {
            LogResetPasswordFailed(string.Join(", ", result.Errors.Select(e => e.Description)));
            return Problem(
                title: "Password Reset Failed",
                detail: string.Join("; ", result.Errors.Select(e => e.Description)),
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        // Revoke all refresh tokens for security (Story 7)
        await RefreshTokenService.RevokeAllUserTokensAsync(user.Id);

        LogOk();
        return Ok(new { success = true });
    }

    #endregion

    #region Hooks

    /// <summary>
    /// Hook method called after a user is created. Can be overridden for custom logic.
    /// </summary>
    /// <param name="user"></param>
    protected virtual Task OnUserCreatedAsync(TUser user)
    {
        // Hook for derived classes to implement custom logic after user creation
        return Task.CompletedTask;
    }

    #endregion

    #region Logger Messages

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Debug, "{Location}: Starting {Username}")]
    private partial void LogStartingUsername(string username, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Information, "{Location}: OK")]
    private partial void LogOk([CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Information, "{Location}: OK {Username}")]
    private partial void LogOkUsername(string username, [CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Warning, "{Location}: Login failed {Username} {Reason}")]
    private partial void LogLoginFailedUsername(string username, string reason, [CallerMemberName] string? location = null);

    [LoggerMessage(6, LogLevel.Warning, "{Location}: Signup failed {Username} {Errors}")]
    private partial void LogSignupFailedUsername(string username, string errors, [CallerMemberName] string? location = null);

    [LoggerMessage(7, LogLevel.Warning, "{Location}: Unauthorized {Reason}")]
    private partial void LogSessionUnauthorized(string reason, [CallerMemberName] string? location = null);

    [LoggerMessage(8, LogLevel.Warning, "{Location}: Unauthorized {Username}")]
    private partial void LogSessionUnauthorizedUsername(string username, [CallerMemberName] string? location = null);

    [LoggerMessage(9, LogLevel.Warning, "{Location}: Invalid refresh token")]
    private partial void LogRefreshTokenInvalid([CallerMemberName] string? location = null);

    [LoggerMessage(10, LogLevel.Warning, "{Location}: No such user {UserId}")]
    private partial void LogRefreshTokenNoUser(string userId, [CallerMemberName] string? location = null);

    [LoggerMessage(11, LogLevel.Warning, "{Location}: No IUserNotifier configured")]
    private partial void LogNoUserNotifierConfigured([CallerMemberName] string? location = null);

    [LoggerMessage(12, LogLevel.Warning, "{Location}: Reset password failed {Reason}")]
    private partial void LogResetPasswordFailed(string reason, [CallerMemberName] string? location = null);

    [LoggerMessage(13, LogLevel.Warning, "{Location}: Change password unauthorized {Reason}")]
    private partial void LogChangePasswordUnauthorized(string reason, [CallerMemberName] string? location = null);

    [LoggerMessage(14, LogLevel.Warning, "{Location}: Change password failed {Username} {Errors}")]
    private partial void LogChangePasswordFailed(string username, string errors, [CallerMemberName] string? location = null);

    [LoggerMessage(15, LogLevel.Debug, "{Location}: Found user {UserId}")]
    private partial void LogFoundUser(string userId, [CallerMemberName] string? location = null);

    #endregion
}

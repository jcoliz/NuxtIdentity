using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;
using System.Security.Claims;

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
    IRefreshTokenService refreshTokenService,
    UserManager<TUser> userManager,
    SignInManager<TUser> signInManager,
    ILogger logger,
    IDbContextCleaner dbContextCleaner) : ControllerBase
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
    /// Gets the DbContext cleaner for preventing concurrency issues.
    /// </summary>
    protected IDbContextCleaner DbContextCleaner { get; } = dbContextCleaner;

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

        // Clear the change tracker to prevent DbContext concurrency issues
        // when querying for roles and claims in CreateUserInfoAsync
        DbContextCleaner.ClearChangeTracker();

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
        LogRefreshTokenRevoking(user.UserName ?? "unknown", oldRefreshToken);

        // Revoke old token (token rotation)
        await RefreshTokenService.RevokeRefreshTokenAsync(oldRefreshToken);

        var newAccessToken = await JwtTokenService.GenerateAccessTokenAsync(user);
        var newRefreshToken = await RefreshTokenService.GenerateRefreshTokenAsync(user.Id);

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

        // Get role claims
        var roleClaims = new List<Claim>();
        foreach (var roleName in roles)
        {
            var role = await UserManager.FindByNameAsync(roleName);
            if (role != null)
            {
                var claims = await UserManager.GetClaimsAsync(user);
                roleClaims.AddRange(claims);
            }
        }

        // Combine user claims and role claims, removing duplicates
        var allClaims = userClaims
            .Concat(roleClaims)
            .GroupBy(c => new { c.Type, c.Value })
            .Select(g => g.First())
            .Select(c => new ClaimInfo { Type = c.Type, Value = c.Value })
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
        LogLoginAttempt(request.Username);

        var user = await UserManager.FindByNameAsync(request.Username);
        if (user == null)
        {
            LogLoginFailed(request.Username, "User not found");
            return Problem(
                title: "Authentication Failed",
                detail: "Invalid credentials",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var result = await SignInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            LogLoginFailed(request.Username, "Invalid password");
            return Problem(
                title: "Authentication Failed",
                detail: "Invalid credentials",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var response = await CreateLoginResponseAsync(user);
        LogLoginSuccess(request.Username);
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
        LogSignupAttempt(request.Username);

        var user = new TUser
        {
            UserName = request.Username,
            Email = request.Email
        };

        var result = await UserManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            LogSignupFailed(request.Username, string.Join(", ", result.Errors.Select(e => e.Description)));

            return Problem(
                title: "Registration Failed",
                detail: string.Join("; ", result.Errors.Select(e => e.Description)),
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        await OnUserCreatedAsync(user);

        LogSignupSuccess(request.Username);

        var response = await CreateLoginResponseAsync(user);
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
            LogSessionUnauthorized($"User not found: {username}");
            return Problem(
                title: "User Not Found",
                detail: "The authenticated user no longer exists",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        // Clear the change tracker to prevent DbContext concurrency issues
        DbContextCleaner.ClearChangeTracker();

        var userInfo = await CreateUserInfoAsync(user);
        LogSessionSuccess(username);

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
    [Authorize]
    [ProducesResponseType(typeof(RefreshResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public virtual async Task<IActionResult> RefreshTokens([FromBody] RefreshRequest request)
    {
        var userId = GetCurrentUserId();
        var username = GetCurrentUsername();

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
        {
            LogRefreshNoToken();
            return Problem(
                title: "Authentication Required",
                detail: "No valid authentication token provided",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        LogRefreshTokenChecking(username, request.RefreshToken);

        // Validate the refresh token
        var isValid = await RefreshTokenService.ValidateRefreshTokenAsync(request.RefreshToken, userId);
        if (!isValid)
        {
            LogRefreshTokenInvalid(username, request.RefreshToken);
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
            LogRefreshTokenNoUser(username, request.RefreshToken);
            return Problem(
                title: "User Not Found",
                detail: "The authenticated user no longer exists",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        LogRefreshTokenOk(username, request.RefreshToken);

        var response = await CreateRefreshResponseAsync(user, request.RefreshToken);

        LogRefreshTokenCreated(username, response.Token.RefreshToken);

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Login attempt for user: {username}")]
    private partial void LogLoginAttempt(string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Login successful for user: {username}")]
    private partial void LogLoginSuccess(string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login failed for user: {username}. Reason: {reason}")]
    private partial void LogLoginFailed(string username, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Signup attempt for user: {username}")]
    private partial void LogSignupAttempt(string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Signup successful for user: {username}")]
    private partial void LogSignupSuccess(string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Signup failed for user: {username}. Errors: {errors}")]
    private partial void LogSignupFailed(string username, string errors);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session request for user: {username}")]
    private partial void LogSessionSuccess(string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Session request unauthorized: {reason}")]
    private partial void LogSessionUnauthorized(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Logout requested")]
    private partial void LogLogoutRequested();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token: Not provided")]
    private partial void LogRefreshNoToken();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Refresh Token: Checking {UserName} {RefreshToken} ")]
    private partial void LogRefreshTokenChecking(string userName, string refreshToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh Token: Invalid {UserName} {RefreshToken} ")]
    private partial void LogRefreshTokenInvalid(string userName, string refreshToken);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Refresh Token: Ok {UserName} {RefreshToken} ")]
    private partial void LogRefreshTokenOk(string userName, string refreshToken);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Refresh Token: Revoking {UserName} {RefreshToken} ")]
    private partial void LogRefreshTokenRevoking(string userName, string refreshToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh Token: No such user {UserName} {RefreshToken} ")]
    private partial void LogRefreshTokenNoUser(string userName, string refreshToken);

    [LoggerMessage(Level = LogLevel.Information, Message = "Refresh Token: Created {UserName} {RefreshToken} ")]
    private partial void LogRefreshTokenCreated(string userName, string refreshToken);

    #endregion
}

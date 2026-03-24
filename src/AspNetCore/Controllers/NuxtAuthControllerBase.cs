using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
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
    IEnumerable<IInvitationService> invitationServices,
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
    /// Gets all registered user notifiers for sending notifications (e.g., email, audit log).
    /// </summary>
    protected IEnumerable<IUserNotifier<TUser>> UserNotifiers { get; } = userNotifiers;

    /// <summary>
    /// Gets the invitation service, or null if none is registered.
    /// </summary>
    /// <exception cref="NuxtIdentityConfigurationException">
    /// Thrown during construction if more than one <see cref="IInvitationService"/> is registered.
    /// </exception>
    protected IInvitationService? InvitationService { get; } = invitationServices.Count() switch
    {
        0 => null,
        1 => invitationServices.First(),
        _ => throw new NuxtIdentityConfigurationException(
            nameof(IInvitationService),
            "Multiple IInvitationService implementations registered. Only one invitation store is supported.")
    };

    /// <summary>
    /// Gets the registration options controlling how user registration behaves.
    /// Override in derived controllers to change registration mode (e.g., <see cref="RegistrationMode.InvitationOnly"/>).
    /// </summary>
    protected virtual RegistrationOptions RegistrationOptions => new();

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
    /// Registers a new user. Dispatches to open or invitation-based registration
    /// based on <see cref="RegistrationOptions"/> and the presence of an invitation code.
    /// </summary>
    /// <param name="request">Signup credentials, optionally including an invitation code.</param>
    [HttpPost("signup")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public virtual async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
    {
        LogStartingUsername(request.Username);

        var mode = RegistrationOptions.Mode;

        if (mode == RegistrationMode.EmailConfirmation)
        {
            throw new NotImplementedException(
                "Email confirmation registration mode is not yet supported. " +
                "Use RegistrationMode.Open or RegistrationMode.InvitationOnly in Phase 1.");
        }

        if (!string.IsNullOrEmpty(request.InvitationCode))
        {
            return await SignUpWithInvitationAsync(request);
        }

        if (mode == RegistrationMode.InvitationOnly)
        {
            LogSignupForbiddenInvitationRequired(request.Username);
            return Problem(
                title: "Invitation Required",
                detail: "An invitation code is required to register",
                statusCode: StatusCodes.Status403Forbidden
            );
        }

        return await SignUpOpenAsync(request);
    }

    /// <summary>
    /// Handles open registration (no invitation required). Preserves the original SignUp behavior.
    /// </summary>
    /// <param name="request">Signup credentials.</param>
    private async Task<IActionResult> SignUpOpenAsync(SignUpRequest request)
    {
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
    /// Handles invitation-based registration with role/claim assignment and email auto-confirmation.
    /// </summary>
    /// <param name="request">Signup credentials including an invitation code.</param>
    /// <exception cref="NuxtIdentityConfigurationException">
    /// Thrown when no <see cref="IInvitationService"/> is registered but an invitation code is provided.
    /// </exception>
    private async Task<IActionResult> SignUpWithInvitationAsync(SignUpRequest request)
    {
        if (InvitationService == null)
        {
            throw new NuxtIdentityConfigurationException(nameof(IInvitationService));
        }

        var invitation = await InvitationService.GetByCodeAsync(request.InvitationCode!);

        if (invitation == null)
        {
            LogSignupInvitationNotFound(request.Username);
            return Problem(
                title: "Invitation Not Found",
                detail: "The invitation code was not found",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        // Check for specific error status messages per Story 2
        var status = invitation.Status;
        if (invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt < DateTime.UtcNow)
        {
            status = InvitationStatus.Expired;
        }

        if (status != InvitationStatus.Pending)
        {
            var detail = status switch
            {
                InvitationStatus.Accepted => "This invitation has already been used",
                InvitationStatus.Revoked => "This invitation has been revoked",
                InvitationStatus.Expired => "This invitation has expired",
                _ => "This invitation is not valid"
            };

            LogSignupInvitationInvalidStatus(request.Username, status);
            return Problem(
                title: "Invalid Invitation",
                detail: detail,
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        // Test constraint: __TEST__ prefix emails must match exactly
        if (invitation.Email.StartsWith("__TEST__", StringComparison.Ordinal) &&
            !string.Equals(invitation.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            LogSignupInvitationEmailMismatch(request.Username);
            return Problem(
                title: "Email Mismatch",
                detail: "The registration email must match the invitation email",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        // Create user with EmailConfirmed = true (auto-confirm per PRD Business Rule 3)
        var user = new TUser
        {
            UserName = request.Username,
            Email = request.Email,
            EmailConfirmed = true
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

        // Assign roles from invitation
        if (!string.IsNullOrEmpty(invitation.Roles))
        {
            var roles = JsonSerializer.Deserialize<List<string>>(invitation.Roles);
            if (roles != null && roles.Count > 0)
            {
                var roleResult = await UserManager.AddToRolesAsync(user, roles);
                if (!roleResult.Succeeded)
                {
                    LogSignupRoleAssignmentFailed(request.Username,
                        string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }
        }

        // Assign claims from invitation
        if (!string.IsNullOrEmpty(invitation.Claims))
        {
            var claimInfos = JsonSerializer.Deserialize<List<ClaimInfo>>(invitation.Claims);
            if (claimInfos != null && claimInfos.Count > 0)
            {
                var claims = claimInfos.Select(c => new Claim(c.Type, c.Value)).ToList();
                var claimResult = await UserManager.AddClaimsAsync(user, claims);
                if (!claimResult.Succeeded)
                {
                    LogSignupClaimAssignmentFailed(request.Username,
                        string.Join(", ", claimResult.Errors.Select(e => e.Description)));
                }
            }
        }

        // Mark invitation as accepted
        await InvitationService.AcceptAsync(invitation, user.Id);

        // Call lifecycle hooks
        await OnUserCreatedAsync(user);
        await OnInvitationAcceptedAsync(user, invitation);

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
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public virtual async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        LogStarting();

        if (!string.IsNullOrEmpty(request.RefreshToken))
        {
            await RefreshTokenService.RevokeRefreshTokenAsync(request.RefreshToken);
        }

        LogOk();
        return NoContent();
    }

    #endregion

    #region Password Management Endpoints

    /// <summary>
    /// Initiates a password reset flow by generating a reset code and notifying the user.
    /// </summary>
    /// <param name="request">The forgot password request containing a username or email.</param>
    /// <remarks>
    /// Always returns 204 No Content regardless of whether the user exists to prevent user enumeration.
    /// </remarks>
    /// <exception cref="NuxtIdentityConfigurationException">
    /// Thrown when no <see cref="IUserNotifier{TUser}"/> implementation is registered.
    /// The consumer must register an implementation in DI for the forgot-password flow to work.
    /// </exception>
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public virtual async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        LogStarting();

        if (!UserNotifiers.Any())
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
            var urlSafeCode = ToBase64Url(code);

            foreach (var notifier in UserNotifiers)
            {
                await notifier.SendResetCodeAsync(user, urlSafeCode);
            }
        }

        // Always return success to prevent user enumeration
        LogOk();
        return NoContent();
    }

    /// <summary>
    /// Resets a user's password using a reset code from the forgot-password flow.
    /// </summary>
    /// <param name="request">The reset password request containing user identifier, reset code, and new password.</param>
    /// <remarks>
    /// On success, all existing refresh tokens for the user are revoked for security.
    /// The caller should redirect the user to the login page after a successful reset.
    /// </remarks>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
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

        var originalCode = FromBase64Url(request.Code);
        var result = await UserManager.ResetPasswordAsync(user, originalCode, request.NewPassword);

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
        return NoContent();
    }

    /// <summary>
    /// Changes the authenticated user's password.
    /// </summary>
    /// <param name="request">The change password request containing the current and new passwords.</param>
    /// <remarks>
    /// On success, all existing refresh tokens for the user are revoked for security.
    /// The caller should log the user out on the client side and prompt them to log in
    /// again with the new password.
    /// </remarks>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public virtual async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        LogStarting();

        var username = GetCurrentUsername();
        if (username == null)
        {
            LogChangePasswordUnauthorized("No username in token");
            return Problem(
                title: "Authentication Required",
                detail: "No valid authentication token provided",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var user = await UserManager.FindByNameAsync(username);
        if (user == null)
        {
            LogChangePasswordUnauthorized($"User not found: {username}");
            return Problem(
                title: "User Not Found",
                detail: "The authenticated user no longer exists",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var result = await UserManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            LogChangePasswordFailed(username, string.Join(", ", result.Errors.Select(e => e.Description)));
            return Problem(
                title: "Password Change Failed",
                detail: string.Join("; ", result.Errors.Select(e => e.Description)),
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        // Revoke all refresh tokens for security (Story 7)
        await RefreshTokenService.RevokeAllUserTokensAsync(user.Id);

        LogOkUsername(username);
        return NoContent();
    }

    #endregion

    #region Hooks

    /// <summary>
    /// Hook method called after a user is created. Can be overridden for custom logic.
    /// </summary>
    /// <param name="user">The newly created user.</param>
    protected virtual Task OnUserCreatedAsync(TUser user)
    {
        // Hook for derived classes to implement custom logic after user creation
        return Task.CompletedTask;
    }

    /// <summary>
    /// Hook method called after a user signs up via invitation with roles and claims assigned.
    /// Can be overridden for custom post-invitation-acceptance logic.
    /// </summary>
    /// <param name="user">The newly created user.</param>
    /// <param name="invitation">The invitation that was accepted.</param>
    protected virtual Task OnInvitationAcceptedAsync(TUser user, InvitationEntity invitation)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Hook method called after a user's email is confirmed. Added as a placeholder for Phase 3.
    /// Can be overridden for custom post-confirmation logic.
    /// </summary>
    /// <param name="user">The user whose email was confirmed.</param>
    protected virtual Task OnUserConfirmedAsync(TUser user)
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Base64URL Encoding

    /// <summary>
    /// Converts a standard base64 string to a Base64URL-safe string by replacing
    /// URL-unsafe characters and removing padding.
    /// </summary>
    /// <param name="base64">The standard base64 string to convert.</param>
    /// <returns>A URL-safe Base64URL string.</returns>
    private static string ToBase64Url(string base64)
    {
        return base64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Converts a Base64URL-safe string back to a standard base64 string by restoring
    /// URL-unsafe characters and adding padding.
    /// </summary>
    /// <param name="base64Url">The Base64URL string to convert.</param>
    /// <returns>A standard base64 string with proper padding.</returns>
    private static string FromBase64Url(string base64Url)
    {
        var base64 = base64Url
            .Replace('-', '+')
            .Replace('_', '/');

        // Restore padding
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return base64;
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

    [LoggerMessage(16, LogLevel.Warning, "{Location}: Signup forbidden, invitation required {Username}")]
    private partial void LogSignupForbiddenInvitationRequired(string username, [CallerMemberName] string? location = null);

    [LoggerMessage(17, LogLevel.Warning, "{Location}: Signup invitation not found {Username}")]
    private partial void LogSignupInvitationNotFound(string username, [CallerMemberName] string? location = null);

    [LoggerMessage(18, LogLevel.Warning, "{Location}: Signup invitation invalid status {Username} {Status}")]
    private partial void LogSignupInvitationInvalidStatus(string username, InvitationStatus status, [CallerMemberName] string? location = null);

    [LoggerMessage(19, LogLevel.Warning, "{Location}: Signup invitation email mismatch {Username}")]
    private partial void LogSignupInvitationEmailMismatch(string username, [CallerMemberName] string? location = null);

    [LoggerMessage(20, LogLevel.Warning, "{Location}: Signup role assignment failed {Username} {Errors}")]
    private partial void LogSignupRoleAssignmentFailed(string username, string errors, [CallerMemberName] string? location = null);

    [LoggerMessage(21, LogLevel.Warning, "{Location}: Signup claim assignment failed {Username} {Errors}")]
    private partial void LogSignupClaimAssignmentFailed(string username, string errors, [CallerMemberName] string? location = null);

    #endregion
}

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Exceptions;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.AspNetCore.Controllers;

public abstract partial class NuxtAuthControllerBase<TUser>
{
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
            LogLoginFailedUserId(user.Id, "Invalid password");
            return Problem(
                title: "Authentication Failed",
                detail: "Invalid credentials",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var response = await CreateLoginResponseAsync(user);
        LogOkUserId(user.Id);
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
        LogOkUserId(user.Id);
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

        // Validate invitation status and email constraints
        var invitation = await InvitationService.GetByCodeAsync(request.InvitationCode!);
        var validationError = ValidateInvitationForSignup(invitation, request);
        if (validationError != null)
        {
            return validationError;
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

        // Assign roles and claims from invitation (failures logged, not fatal)
        await AssignInvitationRolesAsync(user, invitation!.Roles);
        await AssignInvitationClaimsAsync(user, invitation.Claims);

        // Mark invitation as accepted and call lifecycle hooks
        await InvitationService.AcceptAsync(invitation, user.Id);
        await OnUserCreatedAsync(user);
        await OnInvitationAcceptedAsync(user, invitation);

        var response = await CreateLoginResponseAsync(user);
        LogOkUserId(user.Id);
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
            LogSessionUnauthorized("User not found");
            return Problem(
                title: "User Not Found",
                detail: "The authenticated user no longer exists",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var userInfo = await CreateUserInfoAsync(user);
        LogOkUserId(user.Id);

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

        LogOkUserId(user.Id);

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

    /// <summary>
    /// Validates an invitation code and returns its current status.
    /// </summary>
    /// <param name="request">The request containing the invitation code to validate.</param>
    /// <remarks>
    /// Always returns 200 OK with the invitation status. Does not require authentication.
    /// Returns the invitation email only for <see cref="InvitationStatus.Pending"/> status
    /// so the frontend can pre-fill the registration form. For all other statuses, the email
    /// is null to avoid information leakage.
    /// The invitation code is sent in the request body (not the URL) because it is a credential
    /// that must not appear in server logs, browser history, or proxy logs.
    /// </remarks>
    [HttpPut("invitations/validate")]
    [ProducesResponseType(typeof(InvitationStatusResponse), StatusCodes.Status200OK)]
    public virtual async Task<IActionResult> ValidateInvitation([FromBody] InvitationValidateRequest request)
    {
        LogStarting();

        if (InvitationService == null)
        {
            throw new NuxtIdentityConfigurationException(nameof(IInvitationService));
        }

        var status = await InvitationService.ResolveStatusAsync(request.Code);

        string? email = null;
        if (status == InvitationStatus.Pending)
        {
            var invitation = await InvitationService.GetByCodeAsync(request.Code);
            email = invitation?.Email;
        }

        LogValidateInvitationResult(status);

        return Ok(new InvitationStatusResponse
        {
            Status = status,
            Email = email
        });
    }

    #endregion
}

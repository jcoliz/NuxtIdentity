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

    /// <summary>
    /// Validates an invitation code and returns its current status.
    /// </summary>
    /// <param name="code">The invitation code to validate.</param>
    /// <remarks>
    /// Always returns 200 OK with the invitation status. Does not require authentication.
    /// Returns the invitation email only for <see cref="InvitationStatus.Pending"/> status
    /// so the frontend can pre-fill the registration form. For all other statuses, the email
    /// is null to avoid information leakage.
    /// </remarks>
    [HttpGet("invitations/{code}/status")]
    [ProducesResponseType(typeof(InvitationStatusResponse), StatusCodes.Status200OK)]
    public virtual async Task<IActionResult> ValidateInvitation(string code)
    {
        LogStarting();

        if (InvitationService == null)
        {
            throw new NuxtIdentityConfigurationException(nameof(IInvitationService));
        }

        var status = await InvitationService.ResolveStatusAsync(code);

        string? email = null;
        if (status == InvitationStatus.Pending)
        {
            var invitation = await InvitationService.GetByCodeAsync(code);
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

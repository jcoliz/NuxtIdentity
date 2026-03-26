using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Exceptions;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.AspNetCore.Controllers;

public abstract partial class NuxtAuthControllerBase<TUser>
{
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
            LogChangePasswordFailedUserId(user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
            return Problem(
                title: "Password Change Failed",
                detail: string.Join("; ", result.Errors.Select(e => e.Description)),
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        // Revoke all refresh tokens for security (Story 7)
        await RefreshTokenService.RevokeAllUserTokensAsync(user.Id);

        LogOkUserId(user.Id);
        return NoContent();
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
}

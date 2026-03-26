using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.AspNetCore.Controllers;

public abstract partial class NuxtAuthControllerBase<TUser>
{
    #region Logger Messages

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Debug, "{Location}: Starting User {Username}")]
    private partial void LogStartingUsername(string username, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Information, "{Location}: OK")]
    private partial void LogOk([CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Warning, "{Location}: Login failed User {Username} {Reason}")]
    private partial void LogLoginFailedUsername(string username, string reason, [CallerMemberName] string? location = null);

    [LoggerMessage(6, LogLevel.Warning, "{Location}: Signup failed User {Username} {Errors}")]
    private partial void LogSignupFailedUsername(string username, string errors, [CallerMemberName] string? location = null);

    [LoggerMessage(7, LogLevel.Warning, "{Location}: Unauthorized {Reason}")]
    private partial void LogSessionUnauthorized(string reason, [CallerMemberName] string? location = null);

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

    [LoggerMessage(15, LogLevel.Debug, "{Location}: Found user {UserId}")]
    private partial void LogFoundUser(string userId, [CallerMemberName] string? location = null);

    [LoggerMessage(16, LogLevel.Warning, "{Location}: Signup forbidden, invitation required User {Username}")]
    private partial void LogSignupForbiddenInvitationRequired(string username, [CallerMemberName] string? location = null);

    [LoggerMessage(17, LogLevel.Warning, "{Location}: Signup invitation not found User {Username}")]
    private partial void LogSignupInvitationNotFound(string username, [CallerMemberName] string? location = null);

    [LoggerMessage(18, LogLevel.Warning, "{Location}: Signup invitation invalid status User {Username} {Status}")]
    private partial void LogSignupInvitationInvalidStatus(string username, InvitationStatus status, [CallerMemberName] string? location = null);

    [LoggerMessage(19, LogLevel.Warning, "{Location}: Signup invitation email mismatch User {Username}")]
    private partial void LogSignupInvitationEmailMismatch(string username, [CallerMemberName] string? location = null);

    [LoggerMessage(22, LogLevel.Information, "{Location}: Invitation status resolved {Status}")]
    private partial void LogValidateInvitationResult(InvitationStatus status, [CallerMemberName] string? location = null);

    [LoggerMessage(23, LogLevel.Information, "{Location}: OK User {UserId}")]
    private partial void LogOkUserId(string userId, [CallerMemberName] string? location = null);

    [LoggerMessage(24, LogLevel.Debug, "{Location}: Starting User {UserId}")]
    private partial void LogStartingUserId(string userId, [CallerMemberName] string? location = null);

    [LoggerMessage(25, LogLevel.Warning, "{Location}: Login failed User {UserId} {Reason}")]
    private partial void LogLoginFailedUserId(string userId, string reason, [CallerMemberName] string? location = null);

    [LoggerMessage(26, LogLevel.Warning, "{Location}: Change password failed User {UserId} {Errors}")]
    private partial void LogChangePasswordFailedUserId(string userId, string errors, [CallerMemberName] string? location = null);

    [LoggerMessage(27, LogLevel.Warning, "{Location}: Signup role assignment failed User {UserId} {Errors}")]
    private partial void LogSignupRoleAssignmentFailedUserId(string userId, string errors, [CallerMemberName] string? location = null);

    [LoggerMessage(28, LogLevel.Warning, "{Location}: Signup claim assignment failed User {UserId} {Errors}")]
    private partial void LogSignupClaimAssignmentFailedUserId(string userId, string errors, [CallerMemberName] string? location = null);
    #endregion
}

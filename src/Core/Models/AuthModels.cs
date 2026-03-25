namespace NuxtIdentity.Core.Models;

#region Request Models

/// <summary>
/// Request data for user login/authentication.
/// </summary>
public record LoginRequest
{
    /// <summary>
    /// The username for authentication.
    /// </summary>
    public string Username { get; init; } = string.Empty;
    
    /// <summary>
    /// The password for authentication.
    /// </summary>
    public string Password { get; init; } = string.Empty;
}

/// <summary>
/// Request data for new user registration.
/// </summary>
public record SignUpRequest
{
    /// <summary>
    /// The desired username for the new account.
    /// </summary>
    public string Username { get; init; } = string.Empty;
    
    /// <summary>
    /// The email address for the new account.
    /// </summary>
    public string Email { get; init; } = string.Empty;
    
    /// <summary>
    /// The password for the new account.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// An optional invitation code for invitation-based registration.
    /// </summary>
    public string? InvitationCode { get; init; }
}

/// <summary>
/// Request data for refreshing an access token using a refresh token.
/// </summary>
public record RefreshRequest
{
    /// <summary>
    /// The refresh token to exchange for a new access token.
    /// </summary>
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>
/// Request data for initiating a password reset flow.
/// </summary>
public record ForgotPasswordRequest
{
    /// <summary>
    /// The username of the account to reset. Provide either Username or Email.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// The email address of the account to reset. Provide either Username or Email.
    /// </summary>
    public string? Email { get; init; }
}

/// <summary>
/// Request data for resetting a password using a reset code.
/// </summary>
public record ResetPasswordRequest
{
    /// <summary>
    /// The username of the account to reset. Provide either Username or Email.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// The email address of the account to reset. Provide either Username or Email.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// The password reset code received from the forgot-password flow.
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// The new password to set for the account.
    /// </summary>
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>
/// Request data for validating an invitation code.
/// </summary>
public record InvitationValidateRequest
{
    /// <summary>
    /// The invitation code to validate.
    /// </summary>
    public string Code { get; init; } = string.Empty;
}

/// <summary>
/// Request data for changing a password while logged in.
/// </summary>
public record ChangePasswordRequest
{
    /// <summary>
    /// The user's current password for verification.
    /// </summary>
    public string CurrentPassword { get; init; } = string.Empty;

    /// <summary>
    /// The new password to set for the account.
    /// </summary>
    public string NewPassword { get; init; } = string.Empty;
}

#endregion

#region Response Models

/// <summary>
/// Response data returned after successful user login.
/// </summary>
public record LoginResponse
{
    /// <summary>
    /// The JWT access token and refresh token pair.
    /// </summary>
    public TokenPair Token { get; init; } = new();
    
    /// <summary>
    /// Information about the authenticated user.
    /// </summary>
    public UserInfo User { get; init; } = new();
}

/// <summary>
/// Response data returned after successfully refreshing a token.
/// </summary>
public record RefreshResponse
{
    /// <summary>
    /// The new JWT access token and refresh token pair.
    /// </summary>
    public TokenPair Token { get; init; } = new();
}

/// <summary>
/// Response data containing the current session/user information.
/// </summary>
public record SessionResponse
{
    /// <summary>
    /// Information about the currently authenticated user, or null if not authenticated.
    /// </summary>
    public UserInfo? User { get; init; } = new();
}

/// <summary>
/// Response data for the invitation status validation endpoint.
/// </summary>
public record InvitationStatusResponse
{
    /// <summary>
    /// The lifecycle status of the invitation code.
    /// </summary>
    public InvitationStatus Status { get; init; }

    /// <summary>
    /// The invitation email, returned only for <see cref="InvitationStatus.Pending"/> status
    /// so the frontend can pre-fill the registration form. Null for all other statuses to
    /// avoid information leakage.
    /// </summary>
    public string? Email { get; init; }
}

#endregion

#region Data Models

/// <summary>
/// A pair of JWT access token and refresh token.
/// </summary>
public record TokenPair
{
    /// <summary>
    /// The JWT access token used for API authentication.
    /// </summary>
    public string AccessToken { get; init; } = string.Empty;
    
    /// <summary>
    /// The refresh token used to obtain new access tokens.
    /// </summary>
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>
/// Information about a user including identity and authorization data.
/// </summary>
public record UserInfo
{
    /// <summary>
    /// The unique identifier for the user.
    /// </summary>
    public string Id { get; init; } = string.Empty;
    
    /// <summary>
    /// The user's display name or username.
    /// </summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// The user's email address.
    /// </summary>
    public string Email { get; init; } = string.Empty;
    
    /// <summary>
    /// The roles assigned to the user for role-based authorization.
    /// </summary>
    public string[] Roles { get; init; } = [];
    
    /// <summary>
    /// Additional claims associated with the user for claim-based authorization.
    /// </summary>
    public ClaimInfo[] Claims { get; init; } = [];
}

/// <summary>
/// Information about a single claim (key-value pair for authorization).
/// </summary>
public record ClaimInfo
{
    /// <summary>
    /// The claim type (e.g., "department", "subscription_level").
    /// </summary>
    public string Type { get; init; } = string.Empty;
    
    /// <summary>
    /// The claim value (e.g., "engineering", "premium").
    /// </summary>
    public string Value { get; init; } = string.Empty;
}

#endregion

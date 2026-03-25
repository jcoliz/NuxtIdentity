namespace NuxtIdentity.Core.Models;

/// <summary>
/// Represents the lifecycle state of an invitation.
/// </summary>
/// <remarks>
/// <see cref="NotFound"/> is included in the enum rather than being a null/missing concept
/// because the <c>PUT /api/auth/invitations/validate</c> endpoint returns status for all cases
/// including unknown codes. The endpoint always succeeds in answering "what is the status of this code?"
/// <see cref="NotFound"/> is never stored in the database; it is only used in API responses.
/// </remarks>
public enum InvitationStatus
{
    /// <summary>
    /// The invitation code was not found. Used only in API responses, never stored.
    /// </summary>
    NotFound = 0,

    /// <summary>
    /// The invitation has been created and is awaiting acceptance.
    /// </summary>
    Pending,

    /// <summary>
    /// The invitation has been accepted by a user.
    /// </summary>
    Accepted,

    /// <summary>
    /// The invitation has passed its expiration time.
    /// </summary>
    Expired,

    /// <summary>
    /// The invitation has been revoked by an administrator.
    /// </summary>
    Revoked
}

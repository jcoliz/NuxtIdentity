using NuxtIdentity.Core.Models;

namespace NuxtIdentity.Core.Abstractions;

/// <summary>
/// Service for managing invitation lifecycle.
/// </summary>
/// <remarks>
/// The developer injects this into their own admin controller to build invitation management
/// endpoints with their own authorization. The <c>code</c> parameter is <see cref="string"/>
/// (not <see cref="Guid"/>) at the interface boundary. Implementations parse it and return
/// null/<see cref="InvitationStatus.NotFound"/> for invalid formats.
/// </remarks>
public interface IInvitationService
{
    /// <summary>
    /// Creates a new invitation. All parameters are optional with sensible defaults.
    /// </summary>
    /// <param name="email">Optional email address of the invited user. Null when not tied to a specific email.</param>
    /// <param name="roles">Optional roles to assign to the user upon acceptance. Defaults to empty.</param>
    /// <param name="claims">Optional claims to assign to the user upon acceptance. Defaults to empty.</param>
    /// <param name="expiresIn">Optional duration until the invitation expires. Defaults to 30 days.</param>
    /// <param name="metadata">Optional application-specific metadata stored with the invitation.</param>
    /// <param name="status">Optional initial status. Defaults to <see cref="InvitationStatus.Pending"/>. Primarily for testing.</param>
    Task<InvitationEntity> CreateAsync(string? email = null, IReadOnlyList<string>? roles = null,
        IReadOnlyList<ClaimInfo>? claims = null, TimeSpan? expiresIn = null, string? metadata = null,
        InvitationStatus? status = null);

    /// <summary>
    /// Retrieves an invitation by its code.
    /// </summary>
    /// <param name="code">The invitation code as a string.</param>
    /// <returns>The invitation entity if found; otherwise, null.</returns>
    Task<InvitationEntity?> GetByCodeAsync(string code);

    /// <summary>
    /// Returns the effective status of an invitation, accounting for both stored status and time-based expiration.
    /// </summary>
    /// <param name="code">The invitation code as a string.</param>
    /// <returns>The resolved status, including <see cref="InvitationStatus.NotFound"/> for unknown codes.</returns>
    Task<InvitationStatus> ResolveStatusAsync(string code);

    /// <summary>
    /// Validates an invitation code and returns the entity if it is usable (pending and not expired).
    /// </summary>
    /// <param name="code">The invitation code as a string.</param>
    /// <returns>The invitation entity if usable; otherwise, null.</returns>
    Task<InvitationEntity?> ValidateAsync(string code);

    /// <summary>
    /// Marks an invitation as accepted by the specified user.
    /// </summary>
    /// <param name="invitation">The invitation entity to mark as accepted.</param>
    /// <param name="userId">The user ID of the registrant who accepted the invitation.</param>
    Task AcceptAsync(InvitationEntity invitation, string userId);
}

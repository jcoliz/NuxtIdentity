namespace NuxtIdentity.Core.Models;

/// <summary>
/// Represents an invitation to register for the application.
/// </summary>
/// <remarks>
/// <para>
/// Invitations are created by administrators and contain pre-assigned roles and claims
/// that are applied to the user upon acceptance. The <see cref="Code"/> property is the
/// secret shared with the invitee — it should never appear in log output. Use <see cref="Id"/>
/// for all diagnostic logging.
/// </para>
/// <para>
/// Roles and claims are stored as JSON-serialized strings rather than separate join tables,
/// keeping the schema simple. The library stores and delivers this data without complex querying needs.
/// </para>
/// </remarks>
public class InvitationEntity
{
    /// <summary>
    /// Gets or sets the auto-generated identifier. Safe to use in diagnostic logging.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the unique invitation code shared with the invitee.
    /// </summary>
    /// <remarks>
    /// This is a secret value — do not include it in log output. Use <see cref="Id"/> for logging.
    /// Stored as <see cref="Guid"/> for type safety and efficient indexing.
    /// </remarks>
    public Guid Code { get; set; }

    /// <summary>
    /// Gets or sets the optional email address of the invited user.
    /// Null when the invitation is not tied to a specific email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the current lifecycle state of the invitation.
    /// </summary>
    /// <remarks>
    /// <see cref="InvitationStatus.NotFound"/> is never stored; it is only used in API responses.
    /// </remarks>
    public InvitationStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the JSON-serialized list of role names to assign on acceptance.
    /// </summary>
    public string? Roles { get; set; }

    /// <summary>
    /// Gets or sets the JSON-serialized list of <see cref="ClaimInfo"/> type/value pairs to assign on acceptance.
    /// </summary>
    public string? Claims { get; set; }

    /// <summary>
    /// Gets or sets optional JSON string with application-specific data.
    /// </summary>
    /// <remarks>
    /// NuxtIdentity stores and delivers this but does not interpret it.
    /// </remarks>
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets when the invitation was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the invitation expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets when the invitation was accepted, or null if not yet accepted.
    /// </summary>
    public DateTime? AcceptedAt { get; set; }

    /// <summary>
    /// Gets or sets the user ID of the registrant who accepted the invitation, or null if not yet accepted.
    /// </summary>
    public string? AcceptedByUserId { get; set; }
}

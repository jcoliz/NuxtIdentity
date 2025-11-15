namespace NuxtIdentity.Playground.Local.Models;

/// <summary>
/// Represents a refresh token stored in the system.
/// </summary>
public class RefreshTokenEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this token record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the hashed value of the refresh token.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID this token belongs to.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when this token expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets when this token was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets whether this token has been revoked.
    /// </summary>
    public bool IsRevoked { get; set; }
}

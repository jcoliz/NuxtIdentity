using System.Security.Cryptography;

namespace NuxtIdentity.Core.Configuration;

/// <summary>
/// Configuration options for JWT token generation and validation.
/// </summary>
/// <remarks>
/// All security-critical properties (Key, Issuer, Audience) are required and must be configured
/// in appsettings.json. The application will fail to start if these are not properly set.
/// </remarks>
public class JwtOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Gets or sets the secret key used for signing JWT tokens.
    /// </summary>
    /// <remarks>
    /// <para><strong>REQUIRED.</strong> Must be a cryptographically secure random key of at least 256 bits (32 bytes) for HMAC-SHA256.</para>
    ///
    /// <para>When configuring via appsettings.json, provide a Base64-encoded string which will be automatically
    /// decoded to bytes by the .NET configuration system.</para>
    ///
    /// <para><strong>Generating a secure key:</strong></para>
    ///
    /// <para>PowerShell:</para>
    /// <code>
    /// $bytes = [byte[]]::new(32)
    /// [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    /// [Convert]::ToBase64String($bytes)
    /// </code>
    ///
    /// <para>C#:</para>
    /// <code>
    /// var key = RandomNumberGenerator.GetBytes(32);
    /// var base64Key = Convert.ToBase64String(key);
    /// </code>
    ///
    /// <para>Bash (Linux/macOS):</para>
    /// <code>
    /// openssl rand -base64 32
    /// </code>
    /// </remarks>
    public byte[] Key { get; set; } = null!;

    /// <summary>
    /// Gets or sets the issuer of the JWT tokens.
    /// </summary>
    /// <remarks>
    /// <para><strong>REQUIRED.</strong> Identifies who issued the JWT token.</para>
    /// <para>Should be unique to your application (e.g., "my-app-name" or "https://myapp.com").</para>
    /// <para>Used for token validation - tokens with a different issuer will be rejected.</para>
    /// </remarks>
    public string Issuer { get; set; } = null!;

    /// <summary>
    /// Gets or sets the audience for the JWT tokens.
    /// </summary>
    /// <remarks>
    /// <para><strong>REQUIRED.</strong> Identifies who the JWT token is intended for.</para>
    /// <para>Should be unique to your application (e.g., "my-app-users" or "https://myapp.com/api").</para>
    /// <para>Used for token validation - tokens with a different audience will be rejected.</para>
    /// </remarks>
    public string Audience { get; set; } = null!;

    /// <summary>
    /// Gets or sets the token lifespan.
    /// </summary>
    /// <remarks>
    /// If not set (TimeSpan.Zero), falls back to <see cref="ExpirationHours"/>.
    /// Can be configured in appsettings.json as a timespan string (e.g., "01:00:00" for 1 hour, "1.00:00:00" for 1 day).
    /// Default is 1 hour for security best practices.
    /// </remarks>
    public TimeSpan Lifespan { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the refresh token lifespan.
    /// </summary>
    /// <remarks>
    /// <para>Defines how long a refresh token remains valid before it expires.</para>
    /// <para>Can be configured in appsettings.json as a timespan string (e.g., "30.00:00:00" for 30 days).</para>
    /// <para>Default is 30 days, which provides a balance between security and user convenience.</para>
    /// </remarks>
    public TimeSpan RefreshTokenLifespan { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets or sets the token expiration time in hours.
    /// </summary>
    /// <remarks>
    /// This property is deprecated. Use <see cref="Lifespan"/> instead.
    /// Only used as a fallback when <see cref="Lifespan"/> is TimeSpan.Zero.
    /// </remarks>
    [Obsolete("Use Lifespan instead. This property is maintained for backward compatibility.")]
    public int ExpirationHours { get; set; }
}

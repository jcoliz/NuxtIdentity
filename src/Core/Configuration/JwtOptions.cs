namespace NuxtIdentity.Core.Configuration;

/// <summary>
/// Configuration options for JWT token generation and validation.
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Gets or sets the secret key used for signing JWT tokens.
    /// </summary>
    public string Key { get; set; } = "your-secret-key-min-32-characters-long!";

    /// <summary>
    /// Gets or sets the issuer of the JWT tokens.
    /// </summary>
    public string Issuer { get; set; } = "nuxt-identity-playground";

    /// <summary>
    /// Gets or sets the audience for the JWT tokens.
    /// </summary>
    public string Audience { get; set; } = "nuxt-identity-playground";

    /// <summary>
    /// Gets or sets the token lifespan.
    /// </summary>
    /// <remarks>
    /// If not set (TimeSpan.Zero), falls back to <see cref="ExpirationHours"/>.
    /// Can be configured in appsettings.json as a timespan string (e.g., "01:00:00" for 1 hour, "1.00:00:00" for 1 day).
    /// </remarks>
    public TimeSpan Lifespan { get; set; } = TimeSpan.FromHours(1);

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

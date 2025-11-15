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
    /// Gets or sets the token expiration time in hours.
    /// </summary>
    public int ExpirationHours { get; set; } = 1;
}

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using NuxtIdentity.Core.Configuration;

namespace NuxtIdentity.Playground.Local.Configuration;

/// <summary>
/// Configures JWT Bearer authentication options.
/// </summary>
/// <remarks>
/// This class uses the IConfigureNamedOptions pattern to configure JwtBearerOptions
/// after all services are registered. The ConfigureOptions mechanism ensures that
/// the configuration callback runs when the JwtBearerOptions are first accessed by
/// the authentication middleware, which happens after service registration but before
/// the middleware pipeline starts. This avoids the need to call BuildServiceProvider()
/// during startup configuration, which would create duplicate singleton instances.
/// </remarks>
public class JwtBearerOptionsSetup : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtOptions _jwtOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtBearerOptionsSetup"/> class.
    /// </summary>
    /// <param name="jwtOptions">The JWT configuration options.</param>
    public JwtBearerOptionsSetup(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    /// <summary>
    /// Configures the default JWT Bearer authentication options.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    public void Configure(JwtBearerOptions options)
    {
        Configure(JwtBearerDefaults.AuthenticationScheme, options);
    }

    /// <summary>
    /// Configures JWT Bearer authentication options for a named scheme.
    /// </summary>
    /// <param name="name">The name of the authentication scheme.</param>
    /// <param name="options">The options to configure.</param>
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
            return;

        var key = Encoding.UTF8.GetBytes(_jwtOptions.Key);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = _jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    }
}
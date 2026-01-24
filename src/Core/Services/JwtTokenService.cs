using System.IdentityModel.Tokens.Jwt;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Configuration;

namespace NuxtIdentity.Core.Services;

/// <summary>
/// Service for generating and validating JWT tokens.
/// </summary>
/// <typeparam name="TUser">The type of user this service works with.</typeparam>
/// <param name="jwtOptions">JWT configuration options including key, issuer, audience, and expiration settings.</param>
/// <param name="claimsProviders">Collection of claims providers to extract user claims for token generation. Application may provide a custom provider for application-specific claim logic</param>
/// <param name="logger">Logger instance for structured logging of token operations.</param>
/// <param name="timeProvider">Optional time provider for testing time-dependent behavior. Defaults to system time if not provided.</param>
/// <remarks>
/// This is a generic implementation of IJwtTokenService that can work with any user type.
/// The service is designed to be reusable across different applications and user models.
///
/// Design Principles:
///
/// 1. **Generic by Design**: The TUser type parameter allows this service to work with any
///    user model without requiring inheritance or interfaces on the user class itself.
///
/// 2. **Dependency Injection**: The service relies on IUserClaimsProvider&lt;TUser&gt; to extract
///    claims from the user, allowing different implementations for different user types or
///    technologies (ASP.NET Identity, custom user stores, etc.).
///
/// 3. **Configuration via Options Pattern**: JWT settings (key, issuer, audience, expiration)
///    are injected via IOptions&lt;JwtOptions&gt;, following ASP.NET Core best practices.
///
/// 4. **Consistent Validation**: The GetTokenValidationParameters method ensures that the
///    ASP.NET Core authentication middleware validates tokens using the exact same parameters
///    as this service, preventing subtle bugs from configuration mismatches.
///
/// 5. **Structured Logging**: Uses LoggerMessage source generators for high-performance logging
///    of token operations, marked as protected virtual to allow derived classes to customize.
///
/// Library Packaging Strategy:
/// - This class belongs in NuxtIdentity.Core (minimal dependencies)
/// - Only requires System.IdentityModel.Tokens.Jwt and Microsoft.Extensions.Options
/// - No dependency on ASP.NET Identity, Entity Framework, or specific user implementations
/// </remarks>
public partial class JwtTokenService<TUser>(
    IOptions<JwtOptions> jwtOptions,
    IEnumerable<IUserClaimsProvider<TUser>> claimsProviders,
    ILogger<JwtTokenService<TUser>> logger,
    TimeProvider? timeProvider = null) : IJwtTokenService<TUser> where TUser : class
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public async Task<string> GenerateAccessTokenAsync(TUser user)
    {
        // NOTE: User identity claims are provided by IdenityUserClaimsProvider in ASP.NET Identity scenarios.

        var claimsTasks = claimsProviders.Select(provider => provider.GetClaimsAsync(user));
        var claimsArrays = await Task.WhenAll(claimsTasks);
        var claims = claimsArrays.SelectMany(c => c).ToList();

        var username = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? "unknown";

        LogStartingUsername(username);

        // Add standard security claims
        var allClaims = claims.ToList();

        var now = _timeProvider.GetUtcNow();

        // Add issued-at claim for replay attack prevention
        allClaims.Add(new Claim("iat", now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));

        // Optional: Add not-before claim
        allClaims.Add(new Claim("nbf", now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));

        var securityKey = new SymmetricSecurityKey(_jwtOptions.Key);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var expires = _jwtOptions.Lifespan != TimeSpan.Zero
            ? _timeProvider.GetUtcNow().Add(_jwtOptions.Lifespan).DateTime
#pragma warning disable CS0618 // Intentional use of obsolete property for backward compatibility
            : _timeProvider.GetUtcNow().AddHours(_jwtOptions.ExpirationHours).DateTime;
#pragma warning restore CS0618

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: allClaims,  // Use the enhanced claims list
            expires: expires,
            signingCredentials: credentials
        );

        LogOkUsername(username);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc/>
    public Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        LogStarting();

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = GetTokenValidationParameters();

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            LogOk();
            return Task.FromResult<ClaimsPrincipal?>(principal);
        }
        catch (Exception ex)
        {
            LogValidationFailed(ex);
            return Task.FromResult<ClaimsPrincipal?>(null);
        }
    }

    /// <inheritdoc/>
    public TokenValidationParameters GetTokenValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(_jwtOptions.Key),
            ValidateIssuer = true,
            ValidIssuer = _jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    }

    #region Logger Messages

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Debug, "{Location}: Starting {Username}")]
    private partial void LogStartingUsername(string username, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Information, "{Location}: OK")]
    private partial void LogOk([CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Information, "{Location}: OK {Username}")]
    private partial void LogOkUsername(string username, [CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Warning, "{Location}: Validation failed")]
    private partial void LogValidationFailed(Exception ex, [CallerMemberName] string? location = null);

    #endregion
}

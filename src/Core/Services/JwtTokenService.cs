using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NuxtIdentity.Playground.Local.Configuration;

namespace NuxtIdentity.Playground.Local.Services;

/// <summary>
/// Service for generating and validating JWT tokens.
/// </summary>
/// <typeparam name="TUser">The type of user this service works with.</typeparam>
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
public partial class JwtTokenService<TUser> : IJwtTokenService<TUser> where TUser : class
{
    private readonly JwtOptions _jwtOptions;
    private readonly IUserClaimsProvider<TUser> _claimsProvider;
    private readonly ILogger<JwtTokenService<TUser>> _logger;

    public JwtTokenService(
        IOptions<JwtOptions> jwtOptions,
        IUserClaimsProvider<TUser> claimsProvider,
        ILogger<JwtTokenService<TUser>> logger)
    {
        _jwtOptions = jwtOptions.Value;
        _claimsProvider = claimsProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateAccessTokenAsync(TUser user)
    {
        var claims = await _claimsProvider.GetClaimsAsync(user);
        var username = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? "unknown";
        
        LogTokenGenerationStarted(username);

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_jwtOptions.ExpirationHours),
            signingCredentials: credentials
        );

        LogTokenGenerationCompleted(username);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc/>
    public Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        LogTokenValidationStarted();

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = GetTokenValidationParameters();

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            LogTokenValidationCompleted();
            return Task.FromResult<ClaimsPrincipal?>(principal);
        }
        catch (Exception ex)
        {
            LogTokenValidationFailed(ex);
            return Task.FromResult<ClaimsPrincipal?>(null);
        }
    }

    /// <inheritdoc/>
    public TokenValidationParameters GetTokenValidationParameters()
    {
        var key = Encoding.UTF8.GetBytes(_jwtOptions.Key);

        return new TokenValidationParameters
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

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token generation started for user: {username}")]
    private partial void LogTokenGenerationStarted(string username);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token generation completed for user: {username}")]
    private partial void LogTokenGenerationCompleted(string username);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token validation started")]
    private partial void LogTokenValidationStarted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token validation completed")]
    private partial void LogTokenValidationCompleted();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token validation failed")]
    private partial void LogTokenValidationFailed(Exception ex);

    #endregion
}
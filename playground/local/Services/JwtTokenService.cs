using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NuxtIdentity.Playground.Local.Configuration;
using NuxtIdentity.Playground.Local.Models;

namespace NuxtIdentity.Playground.Local.Services;

/// <summary>
/// Service for generating and validating JWT tokens.
/// </summary>
public partial class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(
        IOptions<JwtOptions> jwtOptions,
        UserManager<ApplicationUser> userManager,
        ILogger<JwtTokenService> logger)
    {
        _jwtOptions = jwtOptions.Value;
        _userManager = userManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateAccessTokenAsync(ApplicationUser user)
    {
        LogTokenGenerationStarted(user.UserName ?? "");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? ""),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Sub, user.UserName ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_jwtOptions.ExpirationHours),
            signingCredentials: credentials
        );

        LogTokenGenerationCompleted(user.UserName ?? "");
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc/>
    public Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        LogTokenValidationStarted();

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtOptions.Key);

            var validationParameters = new TokenValidationParameters
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
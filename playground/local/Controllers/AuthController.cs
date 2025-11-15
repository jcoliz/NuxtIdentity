using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace NuxtIdentity.Playground.Local.Controllers;

[ApiController]
[Route("api/auth")]
public partial class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        LogLoginAttempt(request.Username);

        // Simple validation - accept specific credentials for testing
        if (request.Username == "test" && request.Password == "test")
        {
            var token = GenerateJwtToken(request.Username);
            
            LogLoginSuccess(request.Username);
            return Ok(new LoginResponse
            {
                Token = token,
                User = new UserInfo
                {
                    Id = "1",
                    Name = request.Username,
                    Email = $"{request.Username}@example.com"
                }
            });
        }

        LogLoginFailed(request.Username);
        return Unauthorized(new { message = "Invalid credentials" });
    }

    [HttpGet("session")]
    public IActionResult GetSession()
    {
        LogSessionValidationStarted();

        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            LogSessionValidationNoToken();
            return Ok(new { user = (object?)null });
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        
        try
        {
            var principal = ValidateToken(token);
            var username = principal.FindFirst(ClaimTypes.Name)?.Value;
            
            if (username != null)
            {
                LogSessionValidationSuccess(username);
                return Ok(new SessionResponse
                {
                    User = new UserInfo
                    {
                        Id = "1",
                        Name = username,
                        Email = $"{username}@example.com"
                    }
                });
            }
        }
        catch (Exception ex)
        {
            LogSessionValidationFailed(ex);
        }

        return Ok(new { user = (object?)null });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        LogLogoutRequested();
        // In a stateless JWT setup, logout is handled client-side
        // This endpoint exists to match the expected API
        return Ok(new { success = true });
    }

    private string GenerateJwtToken(string username)
    {
        LogTokenGenerationStarted(username);

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _configuration["Jwt:Key"] ?? "your-secret-key-min-32-characters-long!"));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "nuxt-identity-playground",
            audience: _configuration["Jwt:Audience"] ?? "nuxt-identity-playground",
            claims: claims,
            expires: DateTime.Now.AddHours(1),
            signingCredentials: credentials
        );

        LogTokenGenerationCompleted(username);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal ValidateToken(string token)
    {
        LogTokenValidationStarted();

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "your-secret-key-min-32-characters-long!");

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = _configuration["Jwt:Issuer"] ?? "nuxt-identity-playground",
            ValidateAudience = true,
            ValidAudience = _configuration["Jwt:Audience"] ?? "nuxt-identity-playground",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
        LogTokenValidationCompleted();
        return principal;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Login attempt for user: {username}")]
    private partial void LogLoginAttempt(string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Login successful for user: {username}")]
    private partial void LogLoginSuccess(string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login failed for user: {username}")]
    private partial void LogLoginFailed(string username);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token generation started for user: {username}")]
    private partial void LogTokenGenerationStarted(string username);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token generation completed for user: {username}")]
    private partial void LogTokenGenerationCompleted(string username);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Session validation started")]
    private partial void LogSessionValidationStarted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Session validation: no token provided")]
    private partial void LogSessionValidationNoToken();

    [LoggerMessage(Level = LogLevel.Information, Message = "Session validation successful for user: {username}")]
    private partial void LogSessionValidationSuccess(string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Session validation failed")]
    private partial void LogSessionValidationFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token validation started")]
    private partial void LogTokenValidationStarted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token validation completed")]
    private partial void LogTokenValidationCompleted();

    [LoggerMessage(Level = LogLevel.Information, Message = "Logout requested")]
    private partial void LogLogoutRequested();
}

public record LoginRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public record LoginResponse
{
    public string Token { get; init; } = string.Empty;
    public UserInfo User { get; init; } = new();
}

public record SessionResponse
{
    public UserInfo User { get; init; } = new();
}

public record UserInfo
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

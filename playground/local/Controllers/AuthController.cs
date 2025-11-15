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
        if (request.Username == "smith" && request.Password == "hunter2")
        {
            var token = GenerateJwtToken(request.Username);
            
            LogLoginSuccess(request.Username);
            return Ok(new LoginResponse
            {
                Token = new TokenPair
                {
                    AccessToken = token,
                    RefreshToken = GenerateRefreshToken(request.Username)
                },
                User = new UserInfo
                {
                    Id = "1",
                    Name = request.Username,
                    Email = $"{request.Username}@example.com",
                    Role = "admin"
                }
            });
        }

        LogLoginFailed(request.Username);
        return Unauthorized(new { message = "Invalid credentials. Mr. Smith, your password is `hunter2!`" });
    }


    [HttpPost("signup")]
    public IActionResult SignUp([FromBody] SignUpRequest request)
    {
        LogSignUpAttempt(request.Username);

        var token = GenerateJwtToken(request.Username);
        
        LogSignupSuccess(request.Username);
        return Ok(new LoginResponse
        {
            Token = new TokenPair
            {
                AccessToken = token,
                RefreshToken = GenerateRefreshToken(request.Username)
            },
            User = new UserInfo
            {
                Id = "1",
                Name = request.Username,
                Email = $"{request.Username}@example.com",
                Role = "guest"
            }
        });
    }

    private static string GenerateRefreshToken(string username)
    {
        return Guid.NewGuid().ToString("N");
    }

    [HttpPost("refresh")]
    public IActionResult RefreshTokens([FromBody] RefreshRequest request)
    {
        LogRefreshAttempt(request.RefreshToken);

        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            LogRefreshNoToken();
            return Unauthorized();
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        
        try
        {
            var principal = ValidateToken(token);
            var username = principal.FindFirst(ClaimTypes.Name)?.Value;
            
            if (username != null)
            {
                // Validate the refresh token and issue a new access token
                if (IsValidRefreshToken(request.RefreshToken))
                {
                    var newAccessToken = GenerateJwtToken(username);
                    var newRefreshToken = GenerateRefreshToken(username);
                    LogRefreshSuccess(username,newRefreshToken);
                    return Ok(new RefreshResponse()
                    { 
                        Token = new TokenPair
                        {
                            AccessToken = newAccessToken,
                            RefreshToken = newRefreshToken
                        }
                    });
                }

                LogRefreshInvalidToken(username);
                return Unauthorized(new { message = "Invalid refresh token" });
            }
        }
        catch (Exception ex)
        {
            LogRefreshFailed(ex);
        }

        return Unauthorized();
    }

    private bool IsValidRefreshToken(string refreshToken) => true; // Simplified for demonstration

    [HttpGet("user")]
    public IActionResult GetSession()
    {
        LogSessionValidationStarted();

        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            LogSessionValidationNoToken();
            return Unauthorized();
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
                        Email = $"{username}@example.com",
                        Role = "account"
                    }
                });
            }
        }
        catch (Exception ex)
        {
            LogSessionValidationFailed(ex);
        }

        return Unauthorized();
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Refresh token attempt started. Token: {RefreshToken}")]
    private partial void LogRefreshAttempt(string refreshToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token attempt: no token provided")]
    private partial void LogRefreshNoToken();

    [LoggerMessage(Level = LogLevel.Information, Message = "Refresh token successful for user: {username}. New token: {RefreshToken}")]
    private partial void LogRefreshSuccess(string username, string refreshToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token invalid for user: {username}")]
    private partial void LogRefreshInvalidToken(string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token failed")]
    private partial void LogRefreshFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Signup attempt for user: {username}")]
    private partial void LogSignUpAttempt(string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Signup successful for user: {username}")]
    private partial void LogSignupSuccess(string username);
}

public record LoginRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public record SignUpRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public record RefreshRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

public record RefreshResponse
{
    public TokenPair Token { get; init; } = new();
}

public record TokenPair
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
}

public record LoginResponse
{
    public TokenPair Token { get; init; } = new();
    public UserInfo User { get; init; } = new();
}

public record SessionResponse
{
    public UserInfo? User { get; init; } = new();
}

public record UserInfo
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}

public record SubscriptionInfo
{
    public int Id
    {
        get; init;
    }
    public SubscriptionStatus[] Status
    {
        get; init;
    } = [];
};

public enum SubscriptionStatus
{
    Active,
    Inactive,
}
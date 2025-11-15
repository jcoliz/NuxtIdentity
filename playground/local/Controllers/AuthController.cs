using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace NuxtIdentity.Playground.Local.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // Simple validation - accept specific credentials for testing
        if (request.Username == "test" && request.Password == "test")
        {
            var token = GenerateJwtToken(request.Username);
            
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

        return Unauthorized(new { message = "Invalid credentials" });
    }

    [HttpGet("session")]
    public IActionResult GetSession()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Ok(new { user = (object?)null });
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        
        try
        {
            var principal = ValidateToken(token);
            var username = principal.FindFirst(ClaimTypes.Name)?.Value;
            
            if (username != null)
            {
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
        catch
        {
            // Token validation failed
        }

        return Ok(new { user = (object?)null });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // In a stateless JWT setup, logout is handled client-side
        // This endpoint exists to match the expected API
        return Ok(new { success = true });
    }

    private string GenerateJwtToken(string username)
    {
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

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal ValidateToken(string token)
    {
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

        return tokenHandler.ValidateToken(token, validationParameters, out _);
    }
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

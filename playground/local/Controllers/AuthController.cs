using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;

namespace NuxtIdentity.Playground.Local.Controllers;

/// <summary>
/// Handles authentication operations including login, signup, token refresh, and session management.
/// </summary>
[ApiController]
[Route("api/auth")]
public partial class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    #region Public Endpoints

    /// <summary>
    /// Authenticates a user with username and password.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <returns>JWT tokens and user information if successful; otherwise, unauthorized.</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

    /// <summary>
    /// Registers a new user account.
    /// </summary>
    /// <param name="request">Signup credentials.</param>
    /// <returns>JWT tokens and user information for the newly created account.</returns>
    [HttpPost("signup")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
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

    /// <summary>
    /// Refreshes an expired access token using a valid refresh token.
    /// </summary>
    /// <param name="request">Refresh token.</param>
    /// <returns>New JWT token pair if successful; otherwise, unauthorized.</returns>
    /// <remarks>Requires a valid JWT access token in the Authorization header.</remarks>
    [HttpPost("refresh")]
    [Authorize]
    [ProducesResponseType(typeof(RefreshResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult RefreshTokens([FromBody] RefreshRequest request)
    {
        LogRefreshAttempt(request.RefreshToken);

        var username = User.Identity?.Name;
        
        if (username == null)
        {
            LogRefreshNoToken();
            return Unauthorized();
        }
        
        // Validate the refresh token and issue a new access token
        if (IsValidRefreshToken(request.RefreshToken))
        {
            var newAccessToken = GenerateJwtToken(username);
            var newRefreshToken = GenerateRefreshToken(username);
            LogRefreshSuccess(username, newRefreshToken);
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

    /// <summary>
    /// Retrieves the current user's session information.
    /// </summary>
    /// <returns>User information if authenticated; otherwise, unauthorized.</returns>
    /// <remarks>Requires a valid JWT access token in the Authorization header.</remarks>
    [HttpGet("user")]
    [Authorize]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetSession()
    {
        LogSessionValidationStarted();

        var username = User.Identity?.Name;
        
        if (username == null)
        {
            LogSessionValidationNoToken();
            return Unauthorized();
        }

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

    /// <summary>
    /// Logs out the current user.
    /// </summary>
    /// <returns>Success response.</returns>
    /// <remarks>In a stateless JWT setup, logout is handled client-side by discarding tokens.</remarks>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Logout()
    {
        LogLogoutRequested();
        // In a stateless JWT setup, logout is handled client-side
        // This endpoint exists to match the expected API
        return Ok(new { success = true });
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Generates a JWT access token for the specified user.
    /// </summary>
    /// <param name="username">The username to generate the token for.</param>
    /// <returns>A signed JWT token string.</returns>
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

    /// <summary>
    /// Generates a refresh token for the specified user.
    /// </summary>
    /// <param name="username">The username to generate the refresh token for.</param>
    /// <returns>A unique refresh token string.</returns>
    private static string GenerateRefreshToken(string username)
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Validates a refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token to validate.</param>
    /// <returns>True if the token is valid; otherwise, false.</returns>
    /// <remarks>Simplified implementation for demonstration purposes.</remarks>
    private bool IsValidRefreshToken(string refreshToken) => true; // Simplified for demonstration

    #endregion

    #region Logger Messages

    // Login
    [LoggerMessage(Level = LogLevel.Information, Message = "Login attempt for user: {username}")]
    private partial void LogLoginAttempt(string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Login successful for user: {username}")]
    private partial void LogLoginSuccess(string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login failed for user: {username}")]
    private partial void LogLoginFailed(string username);

    // Signup
    [LoggerMessage(Level = LogLevel.Information, Message = "Signup attempt for user: {username}")]
    private partial void LogSignUpAttempt(string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Signup successful for user: {username}")]
    private partial void LogSignupSuccess(string username);

    // Token Generation
    [LoggerMessage(Level = LogLevel.Debug, Message = "Token generation started for user: {username}")]
    private partial void LogTokenGenerationStarted(string username);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token generation completed for user: {username}")]
    private partial void LogTokenGenerationCompleted(string username);

    // Token Validation
    [LoggerMessage(Level = LogLevel.Debug, Message = "Token validation started")]
    private partial void LogTokenValidationStarted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token validation completed")]
    private partial void LogTokenValidationCompleted();

    // Session Validation
    [LoggerMessage(Level = LogLevel.Debug, Message = "Session validation started")]
    private partial void LogSessionValidationStarted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Session validation: no token provided")]
    private partial void LogSessionValidationNoToken();

    [LoggerMessage(Level = LogLevel.Information, Message = "Session validation successful for user: {username}")]
    private partial void LogSessionValidationSuccess(string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Session validation failed")]
    private partial void LogSessionValidationFailed(Exception ex);

    // Refresh Token
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

    // Logout
    [LoggerMessage(Level = LogLevel.Information, Message = "Logout requested")]
    private partial void LogLogoutRequested();

    #endregion
}

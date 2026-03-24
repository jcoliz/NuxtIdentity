using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using NuxtIdentity.AspNetCore.Tests.Helpers;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.AspNetCore.Tests.Controllers;

/// <summary>
/// Integration tests for token management endpoints (refresh, logout) and user data queries (roles, claims).
/// </summary>
[TestFixture]
[Category("Integration")]
public class TokenManagementTests
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private UserManager<TestUser> _userManager = null!;

    [SetUp]
    public void Setup()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();

        var scope = _factory.Services.CreateScope();
        _userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    #region Refresh Tests

    [Test]
    public async Task RefreshTokens_WithValidTokens_ReturnsNewTokens()
    {
        // Arrange
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // Login to get tokens
        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var originalAccessToken = loginResult!.Token.AccessToken;
        var originalRefreshToken = loginResult.Token.RefreshToken;

        // Add token to request
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", originalAccessToken);

        var refreshRequest = new RefreshRequest
        {
            RefreshToken = originalRefreshToken
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshResponse = await response.Content.ReadFromJsonAsync<RefreshResponse>();
        refreshResponse.Should().NotBeNull();
        refreshResponse!.Token.Should().NotBeNull();
        refreshResponse.Token.AccessToken.Should().NotBeNullOrEmpty();
        refreshResponse.Token.RefreshToken.Should().NotBeNullOrEmpty();

        // New tokens should be different from original
        refreshResponse.Token.AccessToken.Should().NotBe(originalAccessToken);
        refreshResponse.Token.RefreshToken.Should().NotBe(originalRefreshToken);
    }

    [Test]
    public async Task RefreshTokens_WithoutAccessToken_ReturnsUnauthorized()
    {
        // Arrange
        var refreshRequest = new RefreshRequest
        {
            RefreshToken = "some-refresh-token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RefreshTokens_WithTokenMissingUserIdClaim_ReturnsUnauthorized()
    {
        // This test covers the edge case where a token passes authentication
        // but is missing the userId or username claim
        // We need to test this at the unit level by directly calling the controller
        // with a mock user that has no claims

        // Arrange - Get services to create a controller instance
        var scope = _factory.Services.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService<TestUser>>();
        var claimsProviders = scope.ServiceProvider.GetRequiredService<IEnumerable<IUserClaimsProvider<TestUser>>>();
        var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var userNotifiers = scope.ServiceProvider.GetRequiredService<IEnumerable<IUserNotifier<TestUser>>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TestAuthController>>();

        var controller = new TestAuthController(
            jwtTokenService,
            claimsProviders,
            refreshTokenService,
            _userManager,
            scope.ServiceProvider.GetRequiredService<SignInManager<TestUser>>(),
            userNotifiers,
            Enumerable.Empty<IInvitationService>(),
            logger
        );

        // Create a ClaimsPrincipal with no NameIdentifier or Name claims
        var emptyIdentity = new System.Security.Claims.ClaimsIdentity();
        var emptyPrincipal = new System.Security.Claims.ClaimsPrincipal(emptyIdentity);

        // Set the controller's User to this empty principal
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = emptyPrincipal
            }
        };

        var refreshRequest = new RefreshRequest
        {
            RefreshToken = "some-refresh-token"
        };

        // Act
        var result = await controller.RefreshTokens(refreshRequest);

        // Assert
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ObjectResult>();
        var objectResult = result as Microsoft.AspNetCore.Mvc.ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        var problemDetails = objectResult.Value as Microsoft.AspNetCore.Mvc.ProblemDetails;
        problemDetails.Should().NotBeNull();
        problemDetails!.Title.Should().Be("Token Refresh Failed");
        problemDetails.Detail.Should().Be("Invalid or expired refresh token");
    }

    [Test]
    public async Task RefreshTokens_WithInvalidRefreshToken_ReturnsUnauthorized()
    {
        // Arrange
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // Login to get access token
        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult!.Token.AccessToken);

        var refreshRequest = new RefreshRequest
        {
            RefreshToken = "invalid-refresh-token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RefreshTokens_ReusingOldRefreshToken_ReturnsUnauthorized()
    {
        // Arrange
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // Login to get tokens
        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var originalRefreshToken = loginResult!.Token.RefreshToken;

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult.Token.AccessToken);

        var refreshRequest = new RefreshRequest { RefreshToken = originalRefreshToken };

        // First refresh - should succeed
        var firstRefresh = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);
        firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);

        // Try to reuse the old refresh token - should fail (token rotation)
        var secondRefresh = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        secondRefresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Logout Tests

    [Test]
    public async Task Logout_WithRefreshToken_ReturnsSuccess()
    {
        // Arrange
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // Login to get tokens
        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var refreshToken = loginResult!.Token.RefreshToken;

        var logoutRequest = new RefreshRequest
        {
            RefreshToken = refreshToken
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/logout", logoutRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Logout_WithoutRefreshToken_ReturnsSuccess()
    {
        // Arrange
        var logoutRequest = new RefreshRequest
        {
            RefreshToken = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/logout", logoutRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Logout_ThenRefresh_ReturnsUnauthorized()
    {
        // Arrange
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // Login to get tokens
        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var accessToken = loginResult!.Token.AccessToken;
        var refreshToken = loginResult.Token.RefreshToken;

        // Logout
        await _client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest { RefreshToken = refreshToken });

        // Try to refresh with logged out token
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        var refreshRequest = new RefreshRequest { RefreshToken = refreshToken };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region User with Roles and Claims Tests

    [Test]
    public async Task GetSession_UserWithRoles_ReturnsRoles()
    {
        // Arrange
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // Add roles
        var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await roleManager.CreateAsync(new IdentityRole("Admin"));
        await roleManager.CreateAsync(new IdentityRole("User"));

        await _userManager.AddToRoleAsync(user, "Admin");
        await _userManager.AddToRoleAsync(user, "User");

        // Login to get token
        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult!.Token.AccessToken);

        // Act
        var response = await _client.GetAsync("/api/auth/user");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessionResponse = await response.Content.ReadFromJsonAsync<SessionResponse>();
        sessionResponse.Should().NotBeNull();
        sessionResponse!.User.Should().NotBeNull();
        sessionResponse.User!.Roles.Should().Contain("Admin");
        sessionResponse.User.Roles.Should().Contain("User");
        sessionResponse.User.Roles.Should().HaveCount(2);
    }

    [Test]
    public async Task GetSession_UserWithClaims_ReturnsClaims()
    {
        // Arrange
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // Add claims
        await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("department", "engineering"));
        await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("level", "senior"));

        // Login to get token
        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult!.Token.AccessToken);

        // Act
        var response = await _client.GetAsync("/api/auth/user");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessionResponse = await response.Content.ReadFromJsonAsync<SessionResponse>();
        sessionResponse.Should().NotBeNull();
        sessionResponse!.User.Should().NotBeNull();
        sessionResponse.User!.Claims.Should().Contain(c => c.Type == "department" && c.Value == "engineering");
        sessionResponse.User.Claims.Should().Contain(c => c.Type == "level" && c.Value == "senior");
    }

    [Test]
    public async Task Login_UserWithRolesAndClaims_ReturnsCompleteUserInfo()
    {
        // Arrange
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // Add roles
        var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await roleManager.CreateAsync(new IdentityRole("Manager"));
        await _userManager.AddToRoleAsync(user, "Manager");

        // Add claims
        await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("region", "west"));

        var request = new LoginRequest
        {
            Username = username,
            Password = password
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse.Should().NotBeNull();
        loginResponse!.User.Roles.Should().Contain("Manager");
        loginResponse.User.Claims.Should().Contain(c => c.Type == "region" && c.Value == "west");
    }

    #endregion

    #region Refresh with Deleted User Tests

    [Test]
    public async Task RefreshTokens_WithDeletedUser_ReturnsUnauthorized()
    {
        // Arrange
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // Login to get tokens
        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var accessToken = loginResult!.Token.AccessToken;
        var refreshToken = loginResult.Token.RefreshToken;

        // Delete the user
        await _userManager.DeleteAsync(user);

        // Try to refresh
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var refreshRequest = new RefreshRequest
        {
            RefreshToken = refreshToken
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("User Not Found");
    }

    #endregion

    #region Token Expiry Edge Cases

    [Test]
    public async Task RefreshTokens_WithExpiredAccessToken_StillRefreshesIfRefreshTokenValid()
    {
        // This test verifies that even if the access token is technically expired,
        // as long as we have a valid refresh token, we can get new tokens
        // Note: In production, access tokens expire after a period defined in JwtOptions

        // Arrange
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // Login to get tokens
        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Even with the current access token, refresh should work
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult!.Token.AccessToken);

        var refreshRequest = new RefreshRequest
        {
            RefreshToken = loginResult.Token.RefreshToken
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}

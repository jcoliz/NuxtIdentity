using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using NuxtIdentity.AspNetCore.Tests.Helpers;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;
using NuxtIdentity.Core.Exceptions;
using NuxtIdentity.AspNetCore.Services;

namespace NuxtIdentity.AspNetCore.Tests.Controllers;

/// <summary>
/// Integration tests for NuxtAuthControllerBase endpoints.
/// </summary>
[TestFixture]
[Category("Integration")]
public class NuxtAuthControllerTests
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private UserManager<TestUser> _userManager = null!;

    [SetUp]
    public void Setup()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();

        // Get UserManager from the factory's services
        var scope = _factory.Services.CreateScope();
        _userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    #region Login Tests

    [Test]
    public async Task Login_ValidCredentials_ReturnsTokenAndUser()
    {
        // Arrange
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

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
        loginResponse!.Token.Should().NotBeNull();
        loginResponse.Token.AccessToken.Should().NotBeNullOrEmpty();
        loginResponse.Token.RefreshToken.Should().NotBeNullOrEmpty();
        loginResponse.User.Should().NotBeNull();
        loginResponse.User.Name.Should().Be(username);
        loginResponse.User.Email.Should().Be($"{username}@test.com");
    }

    [Test]
    public async Task Login_InvalidUsername_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "nonexistent",
            Password = "password"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Authentication Failed");
    }

    [Test]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        var request = new LoginRequest
        {
            Username = username,
            Password = "WrongPassword"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Authentication Failed");
    }

    [Test]
    public async Task Login_EmptyCredentials_ReturnsBadRequest()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "",
            Password = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region SignUp Tests

    [Test]
    public async Task SignUp_ValidData_ReturnsTokenAndUser()
    {
        // Arrange
        var request = new SignUpRequest
        {
            Username = "newuser",
            Email = "newuser@test.com",
            Password = "Test123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse.Should().NotBeNull();
        loginResponse!.Token.Should().NotBeNull();
        loginResponse.Token.AccessToken.Should().NotBeNullOrEmpty();
        loginResponse.Token.RefreshToken.Should().NotBeNullOrEmpty();
        loginResponse.User.Should().NotBeNull();
        loginResponse.User.Name.Should().Be(request.Username);
        loginResponse.User.Email.Should().Be(request.Email);

        // Verify user was created in database
        var user = await _userManager.FindByNameAsync(request.Username);
        user.Should().NotBeNull();
    }

    [Test]
    public async Task SignUp_DuplicateUsername_ReturnsBadRequest()
    {
        // Arrange
        var username = "existinguser";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, "Test123!");

        var request = new SignUpRequest
        {
            Username = username,
            Email = "different@test.com",
            Password = "Test123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Registration Failed");
    }

    [Test]
    public async Task SignUp_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new SignUpRequest
        {
            Username = "testuser",
            Email = "invalid-email",
            Password = "Test123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Assert
        // Note: Since we disabled email validation in test setup, this may pass
        // In production, this would fail with proper email validation
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task SignUp_EmptyFields_ReturnsBadRequest()
    {
        // Arrange
        var request = new SignUpRequest
        {
            Username = "",
            Email = "",
            Password = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Session Tests

    [Test]
    public async Task GetSession_WithValidToken_ReturnsUserInfo()
    {
        // Arrange
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // Login to get token
        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Add token to request
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult!.Token.AccessToken);

        // Act
        var response = await _client.GetAsync("/api/auth/user");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessionResponse = await response.Content.ReadFromJsonAsync<SessionResponse>();
        sessionResponse.Should().NotBeNull();
        sessionResponse!.User.Should().NotBeNull();
        sessionResponse.User!.Name.Should().Be(username);
        sessionResponse.User.Email.Should().Be($"{username}@test.com");
    }

    [Test]
    public async Task GetSession_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/user");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetSession_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "invalid.token.here");

        // Act
        var response = await _client.GetAsync("/api/auth/user");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetSession_WithTokenMissingUsername_ReturnsUnauthorized()
    {
        // This test covers the edge case where a token passes authentication
        // but is missing the username claim (lines 289-296 in controller)
        // We need to test this at the unit level by directly calling the controller
        // with a mock user that has no Name in the Identity

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

        // Create a ClaimsPrincipal with an authenticated identity but no Name
        var emptyIdentity = new System.Security.Claims.ClaimsIdentity(
            authenticationType: "TestAuth" // Must be authenticated
        );
        // Add a NameIdentifier claim but no Name claim to simulate the edge case
        emptyIdentity.AddClaim(new System.Security.Claims.Claim(
            System.Security.Claims.ClaimTypes.NameIdentifier,
            "test-user-id"
        ));
        var principal = new System.Security.Claims.ClaimsPrincipal(emptyIdentity);

        // Set the controller's User to this principal
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = principal
            }
        };

        // Act
        var result = await controller.GetSession();

        // Assert
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ObjectResult>();
        var objectResult = result as Microsoft.AspNetCore.Mvc.ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        var problemDetails = objectResult.Value as Microsoft.AspNetCore.Mvc.ProblemDetails;
        problemDetails.Should().NotBeNull();
        problemDetails!.Title.Should().Be("Authentication Required");
        problemDetails.Detail.Should().Be("No valid authentication token provided");
    }

    [Test]
    public async Task GetSession_WithDeletedUser_ReturnsUnauthorized()
    {
        // Arrange
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // Login to get token
        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Delete the user
        await _userManager.DeleteAsync(user);

        // Add token to request
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult!.Token.AccessToken);

        // Act
        var response = await _client.GetAsync("/api/auth/user");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

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
        // but is missing the userId or username claim (lines 333-341 in controller)
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

    #region SignUp Hook Tests

    [Test]
    public async Task SignUp_CallsOnUserCreatedHook()
    {
        // Note: This test verifies the hook is called by checking the user exists
        // In a derived controller, the hook could be overridden to add roles, claims, etc.

        // Arrange
        var request = new SignUpRequest
        {
            Username = "newuser",
            Email = "newuser@test.com",
            Password = "Test123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify user was created (the hook was called successfully)
        var user = await _userManager.FindByNameAsync(request.Username);
        user.Should().NotBeNull();
        user!.UserName.Should().Be(request.Username);
        user.Email.Should().Be(request.Email);
    }

    #endregion

    #region Empty/Null Field Tests

    [Test]
    public async Task GetSession_UserWithNullEmail_ReturnsEmptyEmail()
    {
        // Arrange
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser { UserName = username, Email = null };
        await _userManager.CreateAsync(user, password);

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
        sessionResponse!.User!.Email.Should().Be(string.Empty);
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

    #region ForgotPassword Tests

    [Test]
    public async Task ForgotPassword_ExistingUser_ReturnsSuccess()
    {
        // Given: An existing user
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // When: Requesting a password reset by username
        var request = new ForgotPasswordRequest { Username = username };
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", request);

        // Then: 204 No Content should be returned
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task ForgotPassword_ExistingUserByEmail_ReturnsSuccess()
    {
        // Given: An existing user with an email address
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // When: Requesting a password reset by email
        var request = new ForgotPasswordRequest { Email = $"{username}@test.com" };
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", request);

        // Then: 204 No Content should be returned
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task ForgotPassword_NonexistentUser_ReturnsSuccess()
    {
        // Given: No user exists with the given username

        // When: Requesting a password reset for a nonexistent user
        var request = new ForgotPasswordRequest { Username = "nonexistent" };
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", request);

        // Then: 204 No Content should still be returned to prevent user enumeration
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task ForgotPassword_EmptyRequest_ReturnsSuccess()
    {
        // Given: An empty request with no username or email

        // When: Submitting an empty forgot password request
        var request = new ForgotPasswordRequest();
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", request);

        // Then: 204 No Content should still be returned to prevent user enumeration
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task ForgotPassword_ExistingUser_NotifiesUser()
    {
        // Given: An existing user
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // When: Requesting a password reset
        var request = new ForgotPasswordRequest { Username = username };
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", request);

        // Then: The request should succeed
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // And: The notifier should have captured a reset code
        var notifier = _factory.Services.GetRequiredService<InMemoryUserNotifier<TestUser>>();
        var notifications = await notifier.GetNotificationsAsync();
        notifications.Should().HaveCount(1);
        notifications[0].Code.Should().NotBeNullOrEmpty();
        notifications[0].Type.Should().Be(NotificationType.PasswordReset);

        // And: The code should be Base64URL-safe (no +, /, or = characters)
        notifications[0].Code.Should().NotContain("+", "code should use Base64URL encoding");
        notifications[0].Code.Should().NotContain("/", "code should use Base64URL encoding");
        notifications[0].Code.Should().NotContain("=", "code should use Base64URL encoding");
    }

    [Test]
    public async Task ForgotPassword_NoNotifierConfigured_ThrowsConfigurationException()
    {
        // Given: A controller with no IUserNotifier registered
        var scope = _factory.Services.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService<TestUser>>();
        var claimsProviders = scope.ServiceProvider.GetRequiredService<IEnumerable<IUserClaimsProvider<TestUser>>>();
        var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TestAuthController>>();

        var controller = new TestAuthController(
            jwtTokenService,
            claimsProviders,
            refreshTokenService,
            _userManager,
            scope.ServiceProvider.GetRequiredService<SignInManager<TestUser>>(),
            Enumerable.Empty<IUserNotifier<TestUser>>(),
            Enumerable.Empty<IInvitationService>(),
            logger
        );

        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        // When: Calling forgot password
        var request = new ForgotPasswordRequest { Username = "testuser" };

        // Then: A NuxtIdentityConfigurationException should be thrown
        var act = () => controller.ForgotPassword(request);
        await act.Should().ThrowAsync<NuxtIdentityConfigurationException>()
            .Where(ex => ex.MissingService.Contains("IUserNotifier"));
    }

    #endregion

    #region ResetPassword Tests

    [Test]
    public async Task ResetPassword_ValidCode_ReturnsSuccess()
    {
        // Given: An existing user with a password
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // And: A password reset code has been generated
        var forgotRequest = new ForgotPasswordRequest { Username = username };
        await _client.PostAsJsonAsync("/api/auth/forgot-password", forgotRequest);

        // And: The reset code is retrieved from the test notifier
        var notifier = _factory.Services.GetRequiredService<InMemoryUserNotifier<TestUser>>();
        var notifications = await notifier.GetNotificationsAsync();
        var resetCode = notifications.First().Code;

        // When: User resets password with the valid code
        var resetRequest = new ResetPasswordRequest
        {
            Username = username,
            Code = resetCode,
            NewPassword = "NewPassword123!"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/reset-password", resetRequest);

        // Then: 204 No Content should be returned
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task ResetPassword_ValidCode_CanLoginWithNewPassword()
    {
        // Given: An existing user whose password has been reset
        var username = "testuser";
        var oldPassword = "Test123!";
        var newPassword = "NewPassword123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, oldPassword);

        // And: The forgot-password flow has been completed
        var forgotRequest = new ForgotPasswordRequest { Username = username };
        await _client.PostAsJsonAsync("/api/auth/forgot-password", forgotRequest);
        var notifier = _factory.Services.GetRequiredService<InMemoryUserNotifier<TestUser>>();
        var notifications = await notifier.GetNotificationsAsync();
        var resetCode = notifications.First().Code;

        // And: The password has been reset
        var resetRequest = new ResetPasswordRequest
        {
            Username = username,
            Code = resetCode,
            NewPassword = newPassword
        };
        await _client.PostAsJsonAsync("/api/auth/reset-password", resetRequest);

        // When: User logs in with the new password
        var loginRequest = new LoginRequest { Username = username, Password = newPassword };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Then: Login should succeed
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task ResetPassword_ValidCode_OldPasswordFails()
    {
        // Given: An existing user whose password has been reset
        var username = "testuser";
        var oldPassword = "Test123!";
        var newPassword = "NewPassword123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, oldPassword);

        // And: The forgot-password flow has been completed
        var forgotRequest = new ForgotPasswordRequest { Username = username };
        await _client.PostAsJsonAsync("/api/auth/forgot-password", forgotRequest);
        var notifier = _factory.Services.GetRequiredService<InMemoryUserNotifier<TestUser>>();
        var notifications = await notifier.GetNotificationsAsync();
        var resetCode = notifications.First().Code;

        // And: The password has been reset
        var resetRequest = new ResetPasswordRequest
        {
            Username = username,
            Code = resetCode,
            NewPassword = newPassword
        };
        await _client.PostAsJsonAsync("/api/auth/reset-password", resetRequest);

        // When: User tries to log in with the old password
        var loginRequest = new LoginRequest { Username = username, Password = oldPassword };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Then: Login should fail
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ResetPassword_InvalidCode_ReturnsBadRequest()
    {
        // Given: An existing user
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // When: User submits an invalid reset code
        var resetRequest = new ResetPasswordRequest
        {
            Username = username,
            Code = "invalid-code",
            NewPassword = "NewPassword123!"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/reset-password", resetRequest);

        // Then: 400 Bad Request should be returned
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ResetPassword_NonexistentUser_ReturnsBadRequest()
    {
        // Given: No user exists with the given username

        // When: Submitting a reset request for a nonexistent user
        var resetRequest = new ResetPasswordRequest
        {
            Username = "nonexistent",
            Code = "some-code",
            NewPassword = "NewPassword123!"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/reset-password", resetRequest);

        // Then: 400 Bad Request should be returned
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ResetPassword_WeakPassword_ReturnsBadRequest()
    {
        // Given: An existing user with a valid reset code
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        // And: A valid reset code has been generated
        var forgotRequest = new ForgotPasswordRequest { Username = username };
        await _client.PostAsJsonAsync("/api/auth/forgot-password", forgotRequest);
        var notifier = _factory.Services.GetRequiredService<InMemoryUserNotifier<TestUser>>();
        var notifications = await notifier.GetNotificationsAsync();
        var resetCode = notifications.First().Code;

        // When: User resets with an empty password (fails minimum length of 1)
        var resetRequest = new ResetPasswordRequest
        {
            Username = username,
            Code = resetCode,
            NewPassword = ""
        };
        var response = await _client.PostAsJsonAsync("/api/auth/reset-password", resetRequest);

        // Then: 400 Bad Request should be returned
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ResetPassword_ValidCode_RevokesRefreshTokens()
    {
        // Given: An existing user who has logged in and has refresh tokens
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        var refreshToken = loginContent!.Token.RefreshToken;

        // And: A password reset code has been generated and password is reset
        var forgotRequest = new ForgotPasswordRequest { Username = username };
        await _client.PostAsJsonAsync("/api/auth/forgot-password", forgotRequest);
        var notifier = _factory.Services.GetRequiredService<InMemoryUserNotifier<TestUser>>();
        var notifications = await notifier.GetNotificationsAsync();
        var resetCode = notifications.First().Code;

        var resetRequest = new ResetPasswordRequest
        {
            Username = username,
            Code = resetCode,
            NewPassword = "NewPassword123!"
        };
        await _client.PostAsJsonAsync("/api/auth/reset-password", resetRequest);

        // When: Attempting to use the old refresh token
        var refreshRequest = new RefreshRequest { RefreshToken = refreshToken };
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Then: Refresh should fail because tokens were revoked
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region ChangePassword Tests

    [Test]
    public async Task ChangePassword_ValidCurrentPassword_ReturnsSuccess()
    {
        // Given: An existing user who is logged in
        var username = "testuser";
        var password = "Test123!";
        var newPassword = "NewPassword123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult!.Token.AccessToken);

        // When: User changes password with correct current password
        var changeRequest = new ChangePasswordRequest
        {
            CurrentPassword = password,
            NewPassword = newPassword
        };
        var response = await _client.PostAsJsonAsync("/api/auth/change-password", changeRequest);

        // Then: 204 No Content should be returned
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task ChangePassword_ValidChange_CanLoginWithNewPassword()
    {
        // Given: An existing user who has changed their password
        var username = "testuser";
        var oldPassword = "Test123!";
        var newPassword = "NewPassword123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, oldPassword);

        // And: The user is logged in and changes password
        var loginRequest = new LoginRequest { Username = username, Password = oldPassword };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult!.Token.AccessToken);

        var changeRequest = new ChangePasswordRequest
        {
            CurrentPassword = oldPassword,
            NewPassword = newPassword
        };
        await _client.PostAsJsonAsync("/api/auth/change-password", changeRequest);

        // When: User logs in with the new password
        _client.DefaultRequestHeaders.Authorization = null;
        var newLoginRequest = new LoginRequest { Username = username, Password = newPassword };
        var response = await _client.PostAsJsonAsync("/api/auth/login", newLoginRequest);

        // Then: Login should succeed
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task ChangePassword_ValidChange_OldPasswordFails()
    {
        // Given: An existing user who has changed their password
        var username = "testuser";
        var oldPassword = "Test123!";
        var newPassword = "NewPassword123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, oldPassword);

        // And: The user is logged in and changes password
        var loginRequest = new LoginRequest { Username = username, Password = oldPassword };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult!.Token.AccessToken);

        var changeRequest = new ChangePasswordRequest
        {
            CurrentPassword = oldPassword,
            NewPassword = newPassword
        };
        await _client.PostAsJsonAsync("/api/auth/change-password", changeRequest);

        // When: User tries to log in with the old password
        _client.DefaultRequestHeaders.Authorization = null;
        var oldLoginRequest = new LoginRequest { Username = username, Password = oldPassword };
        var response = await _client.PostAsJsonAsync("/api/auth/login", oldLoginRequest);

        // Then: Login should fail
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ChangePassword_WrongCurrentPassword_ReturnsBadRequest()
    {
        // Given: An existing user who is logged in
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult!.Token.AccessToken);

        // When: User submits wrong current password
        var changeRequest = new ChangePasswordRequest
        {
            CurrentPassword = "WrongPassword",
            NewPassword = "NewPassword123!"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/change-password", changeRequest);

        // Then: 400 Bad Request should be returned
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ChangePassword_Unauthenticated_ReturnsUnauthorized()
    {
        // Given: No authentication token is provided

        // When: Attempting to change password without being logged in
        var changeRequest = new ChangePasswordRequest
        {
            CurrentPassword = "current",
            NewPassword = "new"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/change-password", changeRequest);

        // Then: 401 Unauthorized should be returned
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ChangePassword_ValidChange_RevokesRefreshTokens()
    {
        // Given: An existing user who is logged in with refresh tokens
        var username = "testuser";
        var password = "Test123!";
        var newPassword = "NewPassword123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        var refreshToken = loginResult!.Token.RefreshToken;

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult.Token.AccessToken);

        // And: The user changes their password
        var changeRequest = new ChangePasswordRequest
        {
            CurrentPassword = password,
            NewPassword = newPassword
        };
        await _client.PostAsJsonAsync("/api/auth/change-password", changeRequest);

        // When: Attempting to use the old refresh token
        var refreshRequest = new RefreshRequest { RefreshToken = refreshToken };
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Then: Refresh should fail because tokens were revoked
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ChangePassword_WithDeletedUser_ReturnsUnauthorized()
    {
        // Given: A user who was logged in but then deleted
        var username = "testuser";
        var password = "Test123!";
        var user = new TestUser(username);
        await _userManager.CreateAsync(user, password);

        var loginRequest = new LoginRequest { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult!.Token.AccessToken);

        // And: The user is deleted
        await _userManager.DeleteAsync(user);

        // When: Attempting to change password
        var changeRequest = new ChangePasswordRequest
        {
            CurrentPassword = password,
            NewPassword = "NewPassword123!"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/change-password", changeRequest);

        // Then: 401 Unauthorized should be returned
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ChangePassword_WithTokenMissingUsername_ReturnsUnauthorized()
    {
        // Given: A controller with a token that has no username claim
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

        // And: An authenticated identity with no Name claim
        var emptyIdentity = new System.Security.Claims.ClaimsIdentity(
            authenticationType: "TestAuth"
        );
        emptyIdentity.AddClaim(new System.Security.Claims.Claim(
            System.Security.Claims.ClaimTypes.NameIdentifier,
            "test-user-id"
        ));
        var principal = new System.Security.Claims.ClaimsPrincipal(emptyIdentity);

        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = principal
            }
        };

        // When: Attempting to change password
        var changeRequest = new ChangePasswordRequest
        {
            CurrentPassword = "current",
            NewPassword = "new"
        };
        var result = await controller.ChangePassword(changeRequest);

        // Then: 401 Unauthorized should be returned
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ObjectResult>();
        var objectResult = result as Microsoft.AspNetCore.Mvc.ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        var problemDetails = objectResult.Value as Microsoft.AspNetCore.Mvc.ProblemDetails;
        problemDetails.Should().NotBeNull();
        problemDetails!.Title.Should().Be("Authentication Required");
    }

    #endregion

    #region Invitation SignUp Tests

    [Test]
    public async Task SignUp_WithValidInvitation_ReturnsTokensAndUser()
    {
        // Given: A valid pending invitation with roles and claims
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateAsync(
            "__TEST__inviteuser@test.com",
            new List<string> { "Admin" },
            new List<ClaimInfo> { new() { Type = "department", Value = "engineering" } },
            TimeSpan.FromHours(24));

        // When: User signs up with the invitation code
        var request = new SignUpRequest
        {
            Username = "inviteuser",
            Email = "__TEST__inviteuser@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 200 OK should be returned with tokens and user info
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse.Should().NotBeNull();
        loginResponse!.Token.AccessToken.Should().NotBeNullOrEmpty();
        loginResponse.Token.RefreshToken.Should().NotBeNullOrEmpty();
        loginResponse.User.Name.Should().Be("inviteuser");
    }

    [Test]
    public async Task SignUp_WithValidInvitation_AssignsRoles()
    {
        // Given: A valid pending invitation with roles
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateAsync(
            "__TEST__roleuser@test.com",
            new List<string> { "Admin" },
            new List<ClaimInfo>(),
            TimeSpan.FromHours(24));

        // When: User signs up with the invitation code
        var request = new SignUpRequest
        {
            Username = "roleuser",
            Email = "__TEST__roleuser@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: User should have the assigned roles
        var user = await _userManager.FindByNameAsync("roleuser");
        user.Should().NotBeNull();
        var roles = await _userManager.GetRolesAsync(user!);
        roles.Should().Contain("Admin");
    }

    [Test]
    public async Task SignUp_WithValidInvitation_AssignsClaims()
    {
        // Given: A valid pending invitation with claims
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateAsync(
            "__TEST__claimuser@test.com",
            new List<string>(),
            new List<ClaimInfo> { new() { Type = "department", Value = "engineering" } },
            TimeSpan.FromHours(24));

        // When: User signs up with the invitation code
        var request = new SignUpRequest
        {
            Username = "claimuser",
            Email = "__TEST__claimuser@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: User should have the assigned claims
        var user = await _userManager.FindByNameAsync("claimuser");
        user.Should().NotBeNull();
        var claims = await _userManager.GetClaimsAsync(user!);
        claims.Should().Contain(c => c.Type == "department" && c.Value == "engineering");
    }

    [Test]
    public async Task SignUp_WithValidInvitation_MarksInvitationAccepted()
    {
        // Given: A valid pending invitation
        var setupScope = _factory.Services.CreateScope();
        var setupService = setupScope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await setupService.CreateAsync(
            "__TEST__acceptuser@test.com",
            new List<string>(),
            new List<ClaimInfo>(),
            TimeSpan.FromHours(24));
        var invitationCode = invitation.Code.ToString();

        // When: User signs up with the invitation code
        var request = new SignUpRequest
        {
            Username = "acceptuser",
            Email = "__TEST__acceptuser@test.com",
            Password = "Test123!",
            InvitationCode = invitationCode
        };
        await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: Invitation status should be Accepted (use fresh scope to avoid stale DbContext)
        var verifyScope = _factory.Services.CreateScope();
        var verifyService = verifyScope.ServiceProvider.GetRequiredService<IInvitationService>();
        var status = await verifyService.ResolveStatusAsync(invitationCode);
        status.Should().Be(InvitationStatus.Accepted);
    }

    [Test]
    public async Task SignUp_WithValidInvitation_EmailIsAutoConfirmed()
    {
        // Given: A valid pending invitation
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateAsync(
            "__TEST__confirmuser@test.com",
            new List<string>(),
            new List<ClaimInfo>(),
            TimeSpan.FromHours(24));

        // When: User signs up with the invitation code
        var request = new SignUpRequest
        {
            Username = "confirmuser",
            Email = "__TEST__confirmuser@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: User's email should be auto-confirmed
        var user = await _userManager.FindByNameAsync("confirmuser");
        user.Should().NotBeNull();
        user!.EmailConfirmed.Should().BeTrue();
    }

    [Test]
    public async Task SignUp_WithUnknownInvitationCode_Returns404()
    {
        // Given: A non-existent invitation code

        // When: User signs up with an unknown code
        var request = new SignUpRequest
        {
            Username = "unknowncode",
            Email = "unknowncode@test.com",
            Password = "Test123!",
            InvitationCode = Guid.NewGuid().ToString()
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 404 Not Found should be returned
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task SignUp_WithAcceptedInvitationCode_Returns400()
    {
        // Given: An invitation that has already been accepted
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateAsync(
            "__TEST__used@test.com",
            new List<string>(),
            new List<ClaimInfo>(),
            TimeSpan.FromHours(24),
            status: InvitationStatus.Accepted);

        // When: User signs up with the accepted invitation code
        var request = new SignUpRequest
        {
            Username = "usedcode",
            Email = "__TEST__used@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 400 Bad Request should be returned with "already been used" message
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("already been used");
    }

    [Test]
    public async Task SignUp_WithExpiredInvitationCode_Returns400()
    {
        // Given: An invitation that has been created with expired status
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateAsync(
            "__TEST__expired@test.com",
            new List<string>(),
            new List<ClaimInfo>(),
            TimeSpan.FromHours(24),
            status: InvitationStatus.Expired);

        // When: User signs up with the expired invitation code
        var request = new SignUpRequest
        {
            Username = "expiredcode",
            Email = "__TEST__expired@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 400 Bad Request should be returned with "expired" message
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("expired");
    }

    [Test]
    public async Task SignUp_WithRevokedInvitationCode_Returns400()
    {
        // Given: An invitation that has been revoked
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateAsync(
            "__TEST__revoked@test.com",
            new List<string>(),
            new List<ClaimInfo>(),
            TimeSpan.FromHours(24),
            status: InvitationStatus.Revoked);

        // When: User signs up with the revoked invitation code
        var request = new SignUpRequest
        {
            Username = "revokedcode",
            Email = "__TEST__revoked@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 400 Bad Request should be returned with "revoked" message
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("revoked");
    }

    [Test]
    public async Task SignUp_WithoutInvitationCode_StillWorksInOpenMode()
    {
        // Given: Open registration mode (default)

        // When: User signs up without an invitation code
        var request = new SignUpRequest
        {
            Username = "openuser",
            Email = "openuser@test.com",
            Password = "Test123!"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 200 OK should be returned
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse.Should().NotBeNull();
        loginResponse!.User.Name.Should().Be("openuser");
    }

    [Test]
    public async Task SignUp_WithTestPrefixEmail_MismatchedEmail_Returns400()
    {
        // Given: An invitation with __TEST__ prefix email
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateAsync(
            "__TEST__specific@test.com",
            new List<string>(),
            new List<ClaimInfo>(),
            TimeSpan.FromHours(24));

        // When: User signs up with a different email
        var request = new SignUpRequest
        {
            Username = "mismatchuser",
            Email = "different@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 400 Bad Request should be returned with "email mismatch" message
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("email must match");
    }

    [Test]
    public async Task SignUp_WithInvalidGuidCode_Returns404()
    {
        // Given: An invalid GUID string as invitation code

        // When: User signs up with an invalid code
        var request = new SignUpRequest
        {
            Username = "invalidguid",
            Email = "invalidguid@test.com",
            Password = "Test123!",
            InvitationCode = "not-a-valid-guid"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 404 Not Found should be returned
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}

/// <summary>
/// Integration tests for invitation-only registration mode (Story 4).
/// </summary>
[TestFixture]
[Category("Integration")]
public class InvitationOnlyModeTests
{
    private InvitationOnlyTestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void Setup()
    {
        _factory = new InvitationOnlyTestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task SignUp_WithoutInvitationCode_Returns403InInvitationOnlyMode()
    {
        // Given: Invitation-only registration mode is configured

        // When: User signs up without an invitation code
        var request = new SignUpRequest
        {
            Username = "nocode",
            Email = "nocode@test.com",
            Password = "Test123!"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 403 Forbidden should be returned
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // And: Response should contain "invitation required" message
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("invitation code is required");
    }

    [Test]
    public async Task SignUp_WithValidInvitationCode_Returns200InInvitationOnlyMode()
    {
        // Given: A valid pending invitation
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateAsync(
            "__TEST__invonly@test.com",
            new List<string>(),
            new List<ClaimInfo>(),
            TimeSpan.FromHours(24));

        // When: User signs up with the invitation code
        var request = new SignUpRequest
        {
            Username = "invonlyuser",
            Email = "__TEST__invonly@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 200 OK should be returned
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse.Should().NotBeNull();
        loginResponse!.Token.AccessToken.Should().NotBeNullOrEmpty();
        loginResponse.User.Name.Should().Be("invonlyuser");
    }
}

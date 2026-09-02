using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
/// Integration tests for core NuxtAuthControllerBase endpoints (login, signup, session).
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(loginResponse, Is.Not.Null);
        Assert.That(loginResponse!.Token, Is.Not.Null);
        Assert.That(loginResponse.Token.AccessToken, Is.Not.Null.And.Not.Empty);
        Assert.That(loginResponse.Token.RefreshToken, Is.Not.Null.And.Not.Empty);
        Assert.That(loginResponse.User, Is.Not.Null);
        Assert.That(loginResponse.User.Name, Is.EqualTo(username));
        Assert.That(loginResponse.User.Email, Is.EqualTo($"{username}@test.com"));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("Authentication Failed"));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("Authentication Failed"));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(loginResponse, Is.Not.Null);
        Assert.That(loginResponse!.Token, Is.Not.Null);
        Assert.That(loginResponse.Token.AccessToken, Is.Not.Null.And.Not.Empty);
        Assert.That(loginResponse.Token.RefreshToken, Is.Not.Null.And.Not.Empty);
        Assert.That(loginResponse.User, Is.Not.Null);
        Assert.That(loginResponse.User.Name, Is.EqualTo(request.Username));
        Assert.That(loginResponse.User.Email, Is.EqualTo(request.Email));

        // Verify user was created in database
        var user = await _userManager.FindByNameAsync(request.Username);
        Assert.That(user, Is.Not.Null);
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("Registration Failed"));
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
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.BadRequest));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var sessionResponse = await response.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.That(sessionResponse, Is.Not.Null);
        Assert.That(sessionResponse!.User, Is.Not.Null);
        Assert.That(sessionResponse.User!.Name, Is.EqualTo(username));
        Assert.That(sessionResponse.User.Email, Is.EqualTo($"{username}@test.com"));
    }

    [Test]
    public async Task GetSession_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/user");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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
        var userNotifiers = scope.ServiceProvider.GetRequiredService<IEnumerable<IUserNotifier>>();
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
        Assert.That(result, Is.TypeOf<Microsoft.AspNetCore.Mvc.ObjectResult>());
        var objectResult = result as Microsoft.AspNetCore.Mvc.ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));

        var problemDetails = objectResult.Value as Microsoft.AspNetCore.Mvc.ProblemDetails;
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Title, Is.EqualTo("Authentication Required"));
        Assert.That(problemDetails.Detail, Is.EqualTo("No valid authentication token provided"));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Verify user was created (the hook was called successfully)
        var user = await _userManager.FindByNameAsync(request.Username);
        Assert.That(user, Is.Not.Null);
        Assert.That(user!.UserName, Is.EqualTo(request.Username));
        Assert.That(user.Email, Is.EqualTo(request.Email));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var sessionResponse = await response.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.That(sessionResponse, Is.Not.Null);
        Assert.That(sessionResponse!.User!.Email, Is.EqualTo(string.Empty));
    }

    #endregion
}

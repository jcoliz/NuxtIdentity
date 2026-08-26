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
using NuxtIdentity.Core.Exceptions;
using NuxtIdentity.Core.Models;
using NuxtIdentity.AspNetCore.Services;

namespace NuxtIdentity.AspNetCore.Tests.Controllers;

/// <summary>
/// Integration tests for password management endpoints (forgot-password, reset-password, change-password).
/// </summary>
[TestFixture]
[Category("Integration")]
public class PasswordManagementTests
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task ForgotPassword_NonexistentUser_ReturnsSuccess()
    {
        // Given: No user exists with the given username

        // When: Requesting a password reset for a nonexistent user
        var request = new ForgotPasswordRequest { Username = "nonexistent" };
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", request);

        // Then: 204 No Content should still be returned to prevent user enumeration
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task ForgotPassword_EmptyRequest_ReturnsSuccess()
    {
        // Given: An empty request with no username or email

        // When: Submitting an empty forgot password request
        var request = new ForgotPasswordRequest();
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", request);

        // Then: 204 No Content should still be returned to prevent user enumeration
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // And: The notifier should have captured a reset code
        var notifier = _factory.Services.GetRequiredService<InMemoryUserNotifier<TestUser>>();
        var notifications = await notifier.GetNotificationsAsync();
        Assert.That(notifications, Has.Count.EqualTo(1));
        Assert.That(notifications[0].Code, Is.Not.Null.And.Not.Empty);
        Assert.That(notifications[0].Type, Is.EqualTo(NotificationType.PasswordReset));

        // And: The code should be Base64URL-safe (no +, /, or = characters)
        Assert.That(notifications[0].Code, Does.Not.Contain("+"));
        Assert.That(notifications[0].Code, Does.Not.Contain("/"));
        Assert.That(notifications[0].Code, Does.Not.Contain("="));
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
        var exception = Assert.ThrowsAsync<NuxtIdentityConfigurationException>(async () => await controller.ForgotPassword(request));
        Assert.That(exception!.MissingService, Does.Contain("IUserNotifier"));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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
        Assert.That(result, Is.TypeOf<Microsoft.AspNetCore.Mvc.ObjectResult>());
        var objectResult = result as Microsoft.AspNetCore.Mvc.ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));

        var problemDetails = objectResult.Value as Microsoft.AspNetCore.Mvc.ProblemDetails;
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Title, Is.EqualTo("Authentication Required"));
    }

    #endregion
}

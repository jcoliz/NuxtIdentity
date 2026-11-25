using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using NuxtIdentity.AspNetCore.Tests.Helpers;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;
using NuxtIdentity.AspNetCore.Models;

namespace NuxtIdentity.AspNetCore.Tests.Controllers;

[TestFixture]
[Category("Integration")]
public class ScalarTokenTests
{
    private TestWebApplicationFactory _factory = null!;
    private TestAuthController _controller = null!;
    private UserManager<TestUser> _userManager = null!;

    [SetUp]
    public void Setup()
    {
        _factory = new TestWebApplicationFactory();
        var scope = _factory.Services.CreateScope();

        _userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        _controller = new TestAuthController(
            scope.ServiceProvider.GetRequiredService<IJwtTokenService<TestUser>>(),
            scope.ServiceProvider.GetRequiredService<IEnumerable<IUserClaimsProvider<TestUser>>>(),
            scope.ServiceProvider.GetRequiredService<IRefreshTokenService>(),
            _userManager,
            scope.ServiceProvider.GetRequiredService<SignInManager<TestUser>>(),
            scope.ServiceProvider.GetRequiredService<IEnumerable<IUserNotifier>>(),
            Enumerable.Empty<IInvitationService>(),
            scope.ServiceProvider.GetRequiredService<ILogger<TestAuthController>>());
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();
    }

    [Test]
    public void ExchangeScalarToken_CancellationRequested_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _controller.ExchangeScalarToken(new ScalarTokenRequest(), cancellation.Token));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task ExchangeScalarToken_MissingGrantType_ReturnsInvalidRequest(string? grantType)
    {
        var result = await _controller.ExchangeScalarToken(
            new ScalarTokenRequest { GrantType = grantType },
            CancellationToken.None);

        var badRequest = result as BadRequestObjectResult;
        Assert.That(badRequest, Is.Not.Null);

        var error = badRequest!.Value as ScalarTokenError;
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Error, Is.EqualTo("invalid_request"));
        Assert.That(error.ErrorDescription, Is.EqualTo("grant_type is required."));
    }

    [Test]
    public async Task ExchangeScalarToken_UnsupportedGrantType_ReturnsUnsupportedGrantType()
    {
        var result = await _controller.ExchangeScalarToken(
            new ScalarTokenRequest { GrantType = "authorization_code" },
            CancellationToken.None);

        var badRequest = result as BadRequestObjectResult;
        Assert.That(badRequest, Is.Not.Null);

        var error = badRequest!.Value as ScalarTokenError;
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Error, Is.EqualTo("unsupported_grant_type"));
    }

    [TestCase(null, null)]
    [TestCase("", "Test123!")]
    [TestCase("testuser", "")]
    [TestCase("   ", "Test123!")]
    [TestCase("testuser", "   ")]
    public async Task ExchangeScalarToken_PasswordGrantMissingCredentials_ReturnsInvalidRequest(
        string? username,
        string? password)
    {
        var result = await _controller.ExchangeScalarToken(
            new ScalarTokenRequest
            {
                GrantType = "password",
                Username = username,
                Password = password
            },
            CancellationToken.None);

        var badRequest = result as BadRequestObjectResult;
        Assert.That(badRequest, Is.Not.Null);

        var error = badRequest!.Value as ScalarTokenError;
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Error, Is.EqualTo("invalid_request"));
        Assert.That(error.ErrorDescription, Is.EqualTo("username and password are required."));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task ExchangeScalarToken_RefreshGrantMissingToken_ReturnsInvalidRequest(string? refreshToken)
    {
        var result = await _controller.ExchangeScalarToken(
            new ScalarTokenRequest
            {
                GrantType = "refresh_token",
                RefreshToken = refreshToken
            },
            CancellationToken.None);

        var badRequest = result as BadRequestObjectResult;
        Assert.That(badRequest, Is.Not.Null);

        var error = badRequest!.Value as ScalarTokenError;
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Error, Is.EqualTo("invalid_request"));
        Assert.That(error.ErrorDescription, Is.EqualTo("refresh_token is required."));
    }

    [Test]
    public async Task ExchangeScalarToken_ValidPasswordGrant_ReturnsScalarTokenResponse()
    {
        var user = new TestUser("scalar-password-user")
        {
            Email = "scalar-password-user@test.com"
        };
        Assert.That(await _userManager.CreateAsync(user, "Test123!"), Is.EqualTo(IdentityResult.Success));

        var result = await _controller.ExchangeScalarToken(
            new ScalarTokenRequest
            {
                GrantType = "password",
                Username = user.UserName,
                Password = "Test123!"
            },
            CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);

        var response = ok!.Value as ScalarTokenResponse;
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.AccessToken, Is.Not.Null.And.Not.Empty);
        Assert.That(response.RefreshToken, Is.Not.Null.And.Not.Empty);
        Assert.That(response.TokenType, Is.EqualTo("Bearer"));
    }

    [Test]
    public async Task ExchangeScalarToken_InvalidPassword_ReturnsInvalidGrant()
    {
        var user = new TestUser("scalar-invalid-password-user")
        {
            Email = "scalar-invalid-password-user@test.com"
        };
        Assert.That(await _userManager.CreateAsync(user, "Test123!"), Is.EqualTo(IdentityResult.Success));

        var result = await _controller.ExchangeScalarToken(
            new ScalarTokenRequest
            {
                GrantType = "password",
                Username = user.UserName,
                Password = "WrongPassword"
            },
            CancellationToken.None);

        var badRequest = result as BadRequestObjectResult;
        Assert.That(badRequest, Is.Not.Null);

        var error = badRequest!.Value as ScalarTokenError;
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Error, Is.EqualTo("invalid_grant"));
    }

    [Test]
    public async Task ExchangeScalarToken_ValidRefreshGrant_RotatesAndReturnsScalarTokenResponse()
    {
        var user = new TestUser("scalar-refresh-user")
        {
            Email = "scalar-refresh-user@test.com"
        };
        Assert.That(await _userManager.CreateAsync(user, "Test123!"), Is.EqualTo(IdentityResult.Success));

        var loginResult = await _controller.Login(new LoginRequest
        {
            Username = user.UserName!,
            Password = "Test123!"
        });
        var loginOk = loginResult as OkObjectResult;
        var loginResponse = loginOk!.Value as LoginResponse;
        var originalRefreshToken = loginResponse!.Token.RefreshToken;

        var result = await _controller.ExchangeScalarToken(
            new ScalarTokenRequest
            {
                GrantType = "refresh_token",
                RefreshToken = originalRefreshToken
            },
            CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);

        var response = ok!.Value as ScalarTokenResponse;
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.AccessToken, Is.Not.Null.And.Not.Empty);
        Assert.That(response.RefreshToken, Is.Not.Null.And.Not.Empty);
        Assert.That(response.RefreshToken, Is.Not.EqualTo(originalRefreshToken));
        Assert.That(response.TokenType, Is.EqualTo("Bearer"));
    }

    [Test]
    public async Task ExchangeScalarToken_InvalidRefreshGrant_ReturnsInvalidGrant()
    {
        var result = await _controller.ExchangeScalarToken(
            new ScalarTokenRequest
            {
                GrantType = "refresh_token",
                RefreshToken = "invalid-refresh-token"
            },
            CancellationToken.None);

        var badRequest = result as BadRequestObjectResult;
        Assert.That(badRequest, Is.Not.Null);

        var error = badRequest!.Value as ScalarTokenError;
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Error, Is.EqualTo("invalid_grant"));
    }
}

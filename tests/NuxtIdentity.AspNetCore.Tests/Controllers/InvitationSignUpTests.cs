using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using NuxtIdentity.AspNetCore.Tests.Helpers;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.AspNetCore.Tests.Controllers;

/// <summary>
/// Integration tests for invitation-based signup endpoints and invitation-only registration mode.
/// </summary>
[TestFixture]
[Category("Integration")]
public class InvitationSignUpTests
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

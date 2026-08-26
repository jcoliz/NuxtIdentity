using System.Net;
using System.Net.Http.Json;
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
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "inviteuser@test.com",
            Roles = """["Admin"]""",
            Claims = """[{"Type":"department","Value":"engineering"}]"""
        });

        // When: User signs up with the invitation code
        var request = new SignUpRequest
        {
            Username = "inviteuser",
            Email = "inviteuser@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 200 OK should be returned with tokens and user info
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(loginResponse, Is.Not.Null);
        Assert.That(loginResponse!.Token.AccessToken, Is.Not.Null.And.Not.Empty);
        Assert.That(loginResponse.Token.RefreshToken, Is.Not.Null.And.Not.Empty);
        Assert.That(loginResponse.User.Name, Is.EqualTo("inviteuser"));
    }

    [Test]
    public async Task SignUp_WithValidInvitation_AssignsRoles()
    {
        // Given: A valid pending invitation with roles
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "roleuser@test.com",
            Roles = """["Admin"]"""
        });

        // When: User signs up with the invitation code
        var request = new SignUpRequest
        {
            Username = "roleuser",
            Email = "roleuser@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: User should have the assigned roles
        var user = await _userManager.FindByNameAsync("roleuser");
        Assert.That(user, Is.Not.Null);
        var roles = await _userManager.GetRolesAsync(user!);
        Assert.That(roles, Does.Contain("Admin"));
    }

    [Test]
    public async Task SignUp_WithValidInvitation_AssignsClaims()
    {
        // Given: A valid pending invitation with claims
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "claimuser@test.com",
            Claims = """[{"Type":"department","Value":"engineering"}]"""
        });

        // When: User signs up with the invitation code
        var request = new SignUpRequest
        {
            Username = "claimuser",
            Email = "claimuser@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: User should have the assigned claims
        var user = await _userManager.FindByNameAsync("claimuser");
        Assert.That(user, Is.Not.Null);
        var claims = await _userManager.GetClaimsAsync(user!);
        Assert.That(claims.Any(c => c.Type == "department" && c.Value == "engineering"), Is.True);
    }

    [Test]
    public async Task SignUp_WithValidInvitation_MarksInvitationAccepted()
    {
        // Given: A valid pending invitation
        var setupScope = _factory.Services.CreateScope();
        var setupService = setupScope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await setupService.CreateTestAsync(new InvitationEntity
        {
            Email = "acceptuser@test.com"
        });
        var invitationCode = invitation.Code.ToString();

        // When: User signs up with the invitation code
        var request = new SignUpRequest
        {
            Username = "acceptuser",
            Email = "acceptuser@test.com",
            Password = "Test123!",
            InvitationCode = invitationCode
        };
        await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: Invitation status should be Accepted (use fresh scope to avoid stale DbContext)
        var verifyScope = _factory.Services.CreateScope();
        var verifyService = verifyScope.ServiceProvider.GetRequiredService<IInvitationService>();
        var status = await verifyService.ResolveStatusAsync(invitationCode);
        Assert.That(status, Is.EqualTo(InvitationStatus.Accepted));
    }

    [Test]
    public async Task SignUp_WithValidInvitation_EmailIsAutoConfirmed()
    {
        // Given: A valid pending invitation
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "confirmuser@test.com"
        });

        // When: User signs up with the invitation code
        var request = new SignUpRequest
        {
            Username = "confirmuser",
            Email = "confirmuser@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: User's email should be auto-confirmed
        var user = await _userManager.FindByNameAsync("confirmuser");
        Assert.That(user, Is.Not.Null);
        Assert.That(user!.EmailConfirmed, Is.True);
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task SignUp_WithAcceptedInvitationCode_Returns400()
    {
        // Given: An invitation that has already been accepted
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "used@test.com",
            Status = InvitationStatus.Accepted
        });

        // When: User signs up with the accepted invitation code
        var request = new SignUpRequest
        {
            Username = "usedcode",
            Email = "used@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 400 Bad Request should be returned with "already been used" message
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("already been used"));
    }

    [Test]
    public async Task SignUp_WithExpiredInvitationCode_Returns400()
    {
        // Given: An invitation that has been created with expired status
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "expired@test.com",
            Status = InvitationStatus.Expired
        });

        // When: User signs up with the expired invitation code
        var request = new SignUpRequest
        {
            Username = "expiredcode",
            Email = "expired@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 400 Bad Request should be returned with "expired" message
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("expired"));
    }

    [Test]
    public async Task SignUp_WithRevokedInvitationCode_Returns400()
    {
        // Given: An invitation that has been revoked
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "revoked@test.com",
            Status = InvitationStatus.Revoked
        });

        // When: User signs up with the revoked invitation code
        var request = new SignUpRequest
        {
            Username = "revokedcode",
            Email = "revoked@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 400 Bad Request should be returned with "revoked" message
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("revoked"));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(loginResponse, Is.Not.Null);
        Assert.That(loginResponse!.User.Name, Is.EqualTo("openuser"));
    }

    [Test]
    public async Task SignUp_WithTestInvitation_MismatchedEmail_Returns400()
    {
        // Given: A test invitation with specific email
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "specific@test.com"
        });

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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("email must match"));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task SignUp_WithInvitation_DuplicateUsername_Returns400()
    {
        // Given: An existing user with the same username
        var user = new TestUser("dupeuser");
        await _userManager.CreateAsync(user, "Test123!");

        // And: A valid pending invitation
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "dupeuser@test.com"
        });

        // When: User signs up with invitation but duplicate username
        var request = new SignUpRequest
        {
            Username = "dupeuser",
            Email = "dupeuser@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 400 Bad Request should be returned with Registration Failed
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("Registration Failed"));
    }

    [Test]
    public async Task SignUp_WithInvitation_WeakPassword_Returns400()
    {
        // Given: A valid pending invitation
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "weakpw@test.com"
        });

        // When: User signs up with an empty password
        var request = new SignUpRequest
        {
            Username = "weakpwuser",
            Email = "weakpw@test.com",
            Password = "",
            InvitationCode = invitation.Code.ToString()
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("Registration Failed"));
    }

    [Test]
    public async Task SignUp_WithInvitation_NonexistentRole_StillSucceeds()
    {
        // Given: A valid pending invitation with a role that doesn't exist
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "badrole@test.com",
            Roles = """["NonExistentRole"]"""
        });

        // When: User signs up with the invitation
        var request = new SignUpRequest
        {
            Username = "badroleuser",
            Email = "badrole@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 200 OK should still be returned (role failure is logged, not fatal)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: User should be created but without the nonexistent role
        var verifyUser = await _userManager.FindByNameAsync("badroleuser");
        Assert.That(verifyUser, Is.Not.Null);
        var roles = await _userManager.GetRolesAsync(verifyUser!);
        Assert.That(roles, Does.Not.Contain("NonExistentRole"));
    }

    [Test]
    public async Task SignUp_WithInvitation_NoRolesOrClaims_StillSucceeds()
    {
        // Given: A valid invitation with no roles and claims
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "noroles@test.com"
        });

        // When: User signs up with the invitation
        var request = new SignUpRequest
        {
            Username = "norolesuser",
            Email = "noroles@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: User should have no extra roles or claims
        var verifyUser = await _userManager.FindByNameAsync("norolesuser");
        Assert.That(verifyUser, Is.Not.Null);
        var roles = await _userManager.GetRolesAsync(verifyUser!);
        Assert.That(roles, Is.Empty);
    }

    [Test]
    public async Task ValidateInvitation_InvalidGuidFormat_ReturnsNotFoundStatus()
    {
        // Given: An invalid GUID string

        // When: Checking the status of an invalid code
        var response = await _client.PutAsJsonAsync("/api/auth/invitations/validate",
            new InvitationValidateRequest { Code = "not-a-valid-guid" });

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Status should be NotFound
        var result = await response.Content.ReadFromJsonAsync<InvitationStatusResponse>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Status, Is.EqualTo(InvitationStatus.NotFound));
        Assert.That(result.Email, Is.Null);
    }

    #endregion

    #region ValidateInvitation Tests (Story 3)

    [Test]
    public async Task ValidateInvitation_PendingInvitation_ReturnsStatusAndEmail()
    {
        // Given: A valid pending invitation
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "validate@test.com"
        });

        // When: Checking the invitation status
        var response = await _client.PutAsJsonAsync("/api/auth/invitations/validate",
            new InvitationValidateRequest { Code = invitation.Code.ToString() });

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Status should be Pending with email visible
        var result = await response.Content.ReadFromJsonAsync<InvitationStatusResponse>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Status, Is.EqualTo(InvitationStatus.Pending));
        Assert.That(result.Email, Is.EqualTo("validate@test.com"));
    }

    [Test]
    public async Task ValidateInvitation_AcceptedInvitation_ReturnsStatusWithoutEmail()
    {
        // Given: An invitation that has been accepted
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "accepted@test.com",
            Status = InvitationStatus.Accepted
        });

        // When: Checking the invitation status
        var response = await _client.PutAsJsonAsync("/api/auth/invitations/validate",
            new InvitationValidateRequest { Code = invitation.Code.ToString() });

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Status should be Accepted with no email
        var result = await response.Content.ReadFromJsonAsync<InvitationStatusResponse>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Status, Is.EqualTo(InvitationStatus.Accepted));
        Assert.That(result.Email, Is.Null);
    }

    [Test]
    public async Task ValidateInvitation_ExpiredInvitation_ReturnsStatusWithoutEmail()
    {
        // Given: An invitation that has expired
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "expired@test.com",
            Status = InvitationStatus.Expired
        });

        // When: Checking the invitation status
        var response = await _client.PutAsJsonAsync("/api/auth/invitations/validate",
            new InvitationValidateRequest { Code = invitation.Code.ToString() });

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Status should be Expired with no email
        var result = await response.Content.ReadFromJsonAsync<InvitationStatusResponse>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Status, Is.EqualTo(InvitationStatus.Expired));
        Assert.That(result.Email, Is.Null);
    }

    [Test]
    public async Task ValidateInvitation_RevokedInvitation_ReturnsStatusWithoutEmail()
    {
        // Given: An invitation that has been revoked
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "revoked@test.com",
            Status = InvitationStatus.Revoked
        });

        // When: Checking the invitation status
        var response = await _client.PutAsJsonAsync("/api/auth/invitations/validate",
            new InvitationValidateRequest { Code = invitation.Code.ToString() });

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Status should be Revoked with no email
        var result = await response.Content.ReadFromJsonAsync<InvitationStatusResponse>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Status, Is.EqualTo(InvitationStatus.Revoked));
        Assert.That(result.Email, Is.Null);
    }

    [Test]
    public async Task ValidateInvitation_UnknownCode_ReturnsNotFoundStatus()
    {
        // Given: A non-existent invitation code

        // When: Checking the status of an unknown code
        var response = await _client.PutAsJsonAsync("/api/auth/invitations/validate",
            new InvitationValidateRequest { Code = Guid.NewGuid().ToString() });

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Status should be NotFound with no email
        var result = await response.Content.ReadFromJsonAsync<InvitationStatusResponse>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Status, Is.EqualTo(InvitationStatus.NotFound));
        Assert.That(result.Email, Is.Null);
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        // And: Response should contain "invitation required" message
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("invitation code is required"));
    }

    [Test]
    public async Task SignUp_WithValidInvitationCode_Returns200InInvitationOnlyMode()
    {
        // Given: A valid pending invitation
        var scope = _factory.Services.CreateScope();
        var invitationService = scope.ServiceProvider.GetRequiredService<IInvitationService>();
        var invitation = await invitationService.CreateTestAsync(new InvitationEntity
        {
            Email = "invonly@test.com"
        });

        // When: User signs up with the invitation code
        var request = new SignUpRequest
        {
            Username = "invonlyuser",
            Email = "invonly@test.com",
            Password = "Test123!",
            InvitationCode = invitation.Code.ToString()
        };
        var response = await _client.PostAsJsonAsync("/api/auth/signup", request);

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(loginResponse, Is.Not.Null);
        Assert.That(loginResponse!.Token.AccessToken, Is.Not.Null.And.Not.Empty);
        Assert.That(loginResponse.User.Name, Is.EqualTo("invonlyuser"));
    }
}

using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using NuxtIdentity.AspNetCore.Extensions;
using NuxtIdentity.AspNetCore.Services;
using NuxtIdentity.AspNetCore.Tests.Helpers;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.AspNetCore.Tests.Services;

[TestFixture]
public sealed class IdentitySeederTests
{
    private SeederHarness _harness = null!;

    [SetUp]
    public async Task SetUp()
    {
        _harness = await SeederHarness.CreateAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _harness.DisposeAsync();
    }

    [Test]
    public async Task SeedAsync_OrdersNamedGroupsAndAddsOnlyMissingData()
    {
        // Given: Two equal-order groups define one user differently and contribute separate roles and claims.
        _harness.Set("IdentitySeeder:Alpha:Order", "10");
        _harness.Set("IdentitySeeder:Alpha:Users:0:UserName", "shared-user");
        _harness.Set("IdentitySeeder:Alpha:Users:0:Email", "alpha@example.com");
        _harness.Set("IdentitySeeder:Alpha:Users:0:Roles:0", "Reader");
        _harness.Set("IdentitySeeder:Alpha:Users:0:Claims:0:Type", "permission");
        _harness.Set("IdentitySeeder:Alpha:Users:0:Claims:0:Value", "read");
        _harness.Set("IdentitySeeder:Alpha:RoleClaims:0:Role", "Reader");
        _harness.Set("IdentitySeeder:Alpha:RoleClaims:0:Claims:0:Type", "permission");
        _harness.Set("IdentitySeeder:Alpha:RoleClaims:0:Claims:0:Value", "read");
        _harness.Set("IdentitySeeder:Alpha:Roles:0:Name", "Reader");

        _harness.Set("IdentitySeeder:Earlier:Order", "5");
        _harness.Set("IdentitySeeder:Earlier:Users:0:UserName", "ordered-user");
        _harness.Set("IdentitySeeder:Earlier:Users:0:Email", "earlier@example.com");

        _harness.Set("IdentitySeeder:Later:Order", "50");
        _harness.Set("IdentitySeeder:Later:Users:0:UserName", "ordered-user");
        _harness.Set("IdentitySeeder:Later:Users:0:Email", "later@example.com");

        _harness.Set("IdentitySeeder:DefaultOrder:Users:0:UserName", "default-order-user");
        _harness.Set("IdentitySeeder:DefaultOrder:Users:0:Email", "default@example.com");

        _harness.Set("IdentitySeeder:ExplicitOrder:Order", "1");
        _harness.Set("IdentitySeeder:ExplicitOrder:Users:0:UserName", "default-order-user");
        _harness.Set("IdentitySeeder:ExplicitOrder:Users:0:Email", "explicit@example.com");

        _harness.Set("IdentitySeeder:Zulu:Order", "10");
        _harness.Set("IdentitySeeder:Zulu:Users:0:UserName", "shared-user");
        _harness.Set("IdentitySeeder:Zulu:Users:0:Email", "zulu@example.com");
        _harness.Set("IdentitySeeder:Zulu:Users:0:Roles:0", "Reader");
        _harness.Set("IdentitySeeder:Zulu:Users:0:Roles:1", "Contributor");
        _harness.Set("IdentitySeeder:Zulu:Users:0:Claims:0:Type", "permission");
        _harness.Set("IdentitySeeder:Zulu:Users:0:Claims:0:Value", "write");
        _harness.Set("IdentitySeeder:Zulu:Users:0:Claims:1:Type", "permission");
        _harness.Set("IdentitySeeder:Zulu:Users:0:Claims:1:Value", "read");
        _harness.Set("IdentitySeeder:Zulu:RoleClaims:0:Role", "Reader");
        _harness.Set("IdentitySeeder:Zulu:RoleClaims:0:Claims:0:Type", "permission");
        _harness.Set("IdentitySeeder:Zulu:RoleClaims:0:Claims:0:Value", "admin");
        _harness.Set("IdentitySeeder:Zulu:RoleClaims:0:Claims:1:Type", "permission");
        _harness.Set("IdentitySeeder:Zulu:RoleClaims:0:Claims:1:Value", "read");
        _harness.Set("IdentitySeeder:Zulu:Roles:0:Name", "Contributor");

        // When: Seeding runs twice.
        await _harness.SeedAsync();
        await _harness.SeedAsync();

        // Then: The case-insensitive group-name tie-breaker selects Alpha's user fields, while distinct claims and memberships are additive and no rows are duplicated.
        await using var scope = _harness.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var user = await users.FindByNameAsync("shared-user");
        Assert.That(user, Is.Not.Null);
        Assert.That(user!.Email, Is.EqualTo("alpha@example.com"));
        Assert.That(await users.GetRolesAsync(user), Is.EquivalentTo(new[] { "Reader", "Contributor" }));
        Assert.That((await users.GetClaimsAsync(user))
            .Where(claim => claim.Type == "permission")
            .Select(claim => claim.Value), Is.EquivalentTo(new[] { "read", "write" }));

        var reader = await roles.FindByNameAsync("Reader");
        Assert.That(reader, Is.Not.Null);
        Assert.That((await roles.GetClaimsAsync(reader!))
            .Where(claim => claim.Type == "permission")
            .Select(claim => claim.Value), Is.EquivalentTo(new[] { "read", "admin" }));
        Assert.That((await users.FindByNameAsync("ordered-user"))!.Email, Is.EqualTo("earlier@example.com"));
        Assert.That((await users.FindByNameAsync("default-order-user"))!.Email, Is.EqualTo("default@example.com"));
        Assert.That(await users.Users.CountAsync(), Is.EqualTo(3));
        Assert.That(await roles.Roles.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task SeedAsync_UserClaimsTargetsExistingUserByNormalizedUsernameWithoutUserDefinition()
    {
        // Given: A database user exists independently of seeding configuration.
        await using (var scope = _harness.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
            var user = new TestUser
            {
                UserName = "Tester",
                Email = "stored@example.com",
                EmailConfirmed = false
            };
            Assert.That((await users.CreateAsync(user, "stored-secret")).Succeeded, Is.True);
            Assert.That((await users.AddClaimAsync(user, new Claim("existing", "preserved"))).Succeeded, Is.True);
        }

        const string entitlement = "f6a1c48d-959b-4cc1-ac0c-e92e4cf49133:Read:Write:Manage";
        _harness.Set("IdentitySeeder:Deployment:UserClaims:0:UserName", "tester");
        _harness.Set("IdentitySeeder:Deployment:UserClaims:0:Claims:0:Type", "entitlement");
        _harness.Set("IdentitySeeder:Deployment:UserClaims:0:Claims:0:Value", entitlement);

        // When: Independent user claims are seeded twice.
        await _harness.SeedAsync();
        await _harness.SeedAsync();

        // Then: Username normalization resolves the user without modifying account fields or duplicating claims.
        await using var verifyScope = _harness.Services.CreateAsyncScope();
        var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var existing = await userManager.FindByNameAsync("Tester");
        Assert.That(existing, Is.Not.Null);
        Assert.That(existing!.UserName, Is.EqualTo("Tester"));
        Assert.That(existing.Email, Is.EqualTo("stored@example.com"));
        Assert.That(existing.EmailConfirmed, Is.False);
        Assert.That(await userManager.CheckPasswordAsync(existing, "stored-secret"), Is.True);
        Assert.That((await userManager.GetClaimsAsync(existing))
            .Select(claim => (claim.Type, claim.Value)),
            Is.EquivalentTo(new[] { ("existing", "preserved"), ("entitlement", entitlement) }));
        Assert.That(await userManager.Users.CountAsync(), Is.EqualTo(1));
        Assert.That(_harness.LogText, Does.Not.Contain("partially completed"));
    }

    [Test]
    public async Task SeedAsync_UserClaimsAreAdditiveAcrossNestedClaimsAndOrderedGroups()
    {
        // Given: Nested and independent claims overlap for a user created in the first group.
        _harness.Set("IdentitySeeder:Accounts:Order", "0");
        _harness.Set("IdentitySeeder:Accounts:Users:0:UserName", "Tester");
        _harness.Set("IdentitySeeder:Accounts:Users:0:Claims:0:Type", "permission");
        _harness.Set("IdentitySeeder:Accounts:Users:0:Claims:0:Value", "read");
        _harness.Set("IdentitySeeder:Accounts:UserClaims:0:UserName", "Tester");
        _harness.Set("IdentitySeeder:Accounts:UserClaims:0:Claims:0:Type", "permission");
        _harness.Set("IdentitySeeder:Accounts:UserClaims:0:Claims:0:Value", "read");
        _harness.Set("IdentitySeeder:Accounts:UserClaims:0:Claims:1:Type", "permission");
        _harness.Set("IdentitySeeder:Accounts:UserClaims:0:Claims:1:Value", "write");
        _harness.Set("IdentitySeeder:Grants:Order", "100");
        _harness.Set("IdentitySeeder:Grants:UserClaims:0:UserName", "Tester");
        _harness.Set("IdentitySeeder:Grants:UserClaims:0:Claims:0:Type", "permission");
        _harness.Set("IdentitySeeder:Grants:UserClaims:0:Claims:0:Value", "write");
        _harness.Set("IdentitySeeder:Grants:UserClaims:0:Claims:1:Type", "permission");
        _harness.Set("IdentitySeeder:Grants:UserClaims:0:Claims:1:Value", "manage");
        _harness.Set("IdentitySeeder:Grants:UserClaims:1:UserName", "Tester");
        _harness.Set("IdentitySeeder:Grants:UserClaims:1:Claims:0:Type", "permission");
        _harness.Set("IdentitySeeder:Grants:UserClaims:1:Claims:0:Value", "manage");

        // When: Seeding runs twice, then a configured claim changes.
        await _harness.SeedAsync();
        await _harness.SeedAsync();
        _harness.Set("IdentitySeeder:Grants:UserClaims:1:Claims:0:Value", "admin");
        await _harness.SeedAsync();

        // Then: Claims are deduplicated across both forms and groups, and changing configuration never revokes grants.
        await using var scope = _harness.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await users.FindByNameAsync("Tester");
        Assert.That(user, Is.Not.Null);
        Assert.That((await users.GetClaimsAsync(user!))
            .Select(claim => (claim.Type, claim.Value)),
            Is.EquivalentTo(new[]
            {
                ("permission", "read"), ("permission", "write"),
                ("permission", "manage"), ("permission", "admin")
            }));
        Assert.That(await users.Users.CountAsync(), Is.EqualTo(1));
        Assert.That(_harness.LogText, Does.Not.Contain("partially completed"));
    }

    [Test]
    public async Task SeedAsync_UserClaimsIsolatesInvalidClaimsAndMissingUsers()
    {
        // Given: Missing and blank usernames, invalid claim fields, and an unavailable target precede valid claims.
        _harness.Set("IdentitySeeder:Application:Users:0:UserName", "valid-user");
        _harness.Set("IdentitySeeder:Application:UserClaims:0:Claims:0:Type", "permission");
        _harness.Set("IdentitySeeder:Application:UserClaims:0:Claims:0:Value", "read");
        _harness.Set("IdentitySeeder:Application:UserClaims:1:UserName", " ");
        _harness.Set("IdentitySeeder:Application:UserClaims:1:Claims:0:Type", "permission");
        _harness.Set("IdentitySeeder:Application:UserClaims:1:Claims:0:Value", "read");
        _harness.Set("IdentitySeeder:Application:UserClaims:2:UserName", "missing-user");
        _harness.Set("IdentitySeeder:Application:UserClaims:2:Claims:0:Type", "permission");
        _harness.Set("IdentitySeeder:Application:UserClaims:2:Claims:0:Value", "read");
        _harness.Set("IdentitySeeder:Application:UserClaims:3:UserName", "valid-user");
        _harness.Set("IdentitySeeder:Application:UserClaims:3:Claims:0:Type", " ");
        _harness.Set("IdentitySeeder:Application:UserClaims:3:Claims:0:Value", "read");
        _harness.Set("IdentitySeeder:Application:UserClaims:3:Claims:1:Type", "permission");
        _harness.Set("IdentitySeeder:Application:UserClaims:3:Claims:2:Type", "permission");
        _harness.Set("IdentitySeeder:Application:UserClaims:3:Claims:2:Value", "read");
        _harness.Set("IdentitySeeder:Later:Order", "100");
        _harness.Set("IdentitySeeder:Later:UserClaims:0:UserName", "valid-user");
        _harness.Set("IdentitySeeder:Later:UserClaims:0:Claims:0:Type", "empty-value");
        _harness.Set("IdentitySeeder:Later:UserClaims:0:Claims:0:Value", "");

        // When: Seeding runs.
        await _harness.SeedAsync();

        // Then: Valid claims continue within and across groups, with precise diagnostics and no implicit account creation.
        await using var scope = _harness.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await users.FindByNameAsync("valid-user");
        Assert.That(user, Is.Not.Null);
        Assert.That((await users.GetClaimsAsync(user!))
            .Select(claim => (claim.Type, claim.Value)),
            Is.EquivalentTo(new[] { ("permission", "read"), ("empty-value", "") }));
        Assert.That(await users.FindByNameAsync("missing-user"), Is.Null);
        Assert.That(await users.Users.CountAsync(), Is.EqualTo(1));
        Assert.That(_harness.LogText, Does.Contain("Username is required for user claim."));
        Assert.That(_harness.LogText, Does.Contain("Claim type is required."));
        Assert.That(_harness.LogText, Does.Contain("Claim value is required."));
        Assert.That(_harness.LogText, Does.Contain("Required user is unavailable."));
        Assert.That(_harness.LogText, Does.Contain("IdentitySeeder:Application:UserClaims:3:Claims:1"));
        Assert.That(_harness.LogText, Does.Contain("Succeeded: 3; failed: 4; skipped: 1"));
    }

    [Test]
    public async Task SeedAsync_AppliesPasswordOnlyWhenCreatingUser()
    {
        // Given: A user is configured with a password and an email.
        _harness.Set("IdentitySeeder:Application:Users:0:UserName", "password-user");
        _harness.Set("IdentitySeeder:Application:Users:0:Email", "initial@example.com");
        _harness.Set("IdentitySeeder:Application:Users:0:Password", "initial-secret");
        await _harness.SeedAsync();

        // When: Configuration changes and seeding runs again.
        _harness.Set("IdentitySeeder:Application:Users:0:Email", "changed@example.com");
        _harness.Set("IdentitySeeder:Application:Users:0:Password", "changed-secret");
        await _harness.SeedAsync();

        // Then: The original password and scalar fields remain unchanged and neither password appears in logs.
        await using var scope = _harness.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByNameAsync("password-user");
        Assert.That(user, Is.Not.Null);
        Assert.That(user!.Email, Is.EqualTo("initial@example.com"));
        Assert.That(await userManager.CheckPasswordAsync(user, "initial-secret"), Is.True);
        Assert.That(await userManager.CheckPasswordAsync(user, "changed-secret"), Is.False);
        Assert.That(_harness.LogText, Does.Not.Contain("initial-secret"));
        Assert.That(_harness.LogText, Does.Not.Contain("changed-secret"));
    }

    [Test]
    public async Task SeedAsync_ContinuesAfterInvalidItemsAndSkipsOnlyUnavailableDependencies()
    {
        // Given: An invalid role, a role rejected by Identity validation, a valid role, and mixed role references.
        _harness.Set("IdentitySeeder:Application:Roles:0:Name", "Invalid");
        _harness.Set("IdentitySeeder:Application:Roles:0:Id", "not-a-guid");
        _harness.Set("IdentitySeeder:Application:Roles:1:Name", "RejectedRole");
        _harness.Set("IdentitySeeder:Application:Roles:2:Name", "Available");
        _harness.Set("IdentitySeeder:Application:Users:0:UserName", "valid-user");
        _harness.Set("IdentitySeeder:Application:Users:0:Roles:0", "Available");
        _harness.Set("IdentitySeeder:Application:Users:0:Roles:1", "Missing");
        _harness.Set("IdentitySeeder:Application:Users:1:UserName", "");
        _harness.Set("IdentitySeeder:Application:Users:2:UserName", "later-user");

        // When: Seeding processes the group.
        await _harness.SeedAsync();

        // Then: Valid work completes, unavailable dependencies are skipped, and the summary reports partial completion.
        await using var scope = _harness.Services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        Assert.That(await roleManager.RoleExistsAsync("Available"), Is.True);
        Assert.That(await userManager.FindByNameAsync("valid-user"), Is.Not.Null);
        Assert.That(await userManager.FindByNameAsync("later-user"), Is.Not.Null);
        Assert.That(_harness.LogText, Does.Contain("partially completed"));
        Assert.That(_harness.LogText, Does.Contain("skipped"));
    }

    [Test]
    public async Task SeedAsync_ValidatesInvitationCodesAndExpiryAndCanonicalizesRoleIds()
    {
        // Given: A role with a non-canonical GUID, malformed and empty invitation codes, and a negative expiry.
        var roleId = Guid.Parse("F7260EFE-A5F8-4A5A-850F-A1F2F8725C78");
        var invalidCode = "malformed-invitation-secret";
        var negativeExpiryCode = Guid.NewGuid();
        _harness.Set("IdentitySeeder:Application:Roles:0:Name", "FixedIdRole");
        _harness.Set("IdentitySeeder:Application:Roles:0:Id", roleId.ToString("B").ToUpperInvariant());
        _harness.Set("IdentitySeeder:Application:Invitations:0:Code", invalidCode);
        _harness.Set("IdentitySeeder:Application:Invitations:1:Code", Guid.Empty.ToString("D"));
        _harness.Set("IdentitySeeder:Application:Invitations:2:Code", negativeExpiryCode.ToString("D"));
        _harness.Set("IdentitySeeder:Application:Invitations:2:ExpiresIn", "-00:00:01");

        // When: Seeding runs.
        await _harness.SeedAsync();

        // Then: The role ID is canonical, invalid invitations are not created, and sensitive code values are absent from logs.
        await using var scope = _harness.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var role = await roles.FindByNameAsync("FixedIdRole");
        Assert.That(role, Is.Not.Null);
        Assert.That(role!.Id, Is.EqualTo(roleId.ToString("D")));
        Assert.That(_harness.InvitationCreateCount, Is.Zero);
        Assert.That(_harness.LogText, Does.Not.Contain(invalidCode));
        Assert.That(_harness.LogText, Does.Not.Contain(negativeExpiryCode.ToString()));
        Assert.That(_harness.LogText, Does.Contain("partially completed"));
    }

    [Test]
    public async Task SeedAsync_CreatesInvitationsWithConfiguredCodeAndLeavesExistingInvitationUnchanged()
    {
        // Given: A configured invitation with a predictable code and grant data.
        var code = Guid.NewGuid();
        _harness.Set("IdentitySeeder:Deployment:Invitations:0:Code", code.ToString("D"));
        _harness.Set("IdentitySeeder:Deployment:Invitations:0:Email", "invitee@example.com");
        _harness.Set("IdentitySeeder:Deployment:Invitations:0:Roles:0", "Reader");
        _harness.Set("IdentitySeeder:Deployment:Invitations:0:Claims:0:Type", "department");
        _harness.Set("IdentitySeeder:Deployment:Invitations:0:Claims:0:Value", "engineering");
        _harness.Set("IdentitySeeder:Deployment:Invitations:0:Metadata", "seeded");
        _harness.Set("IdentitySeeder:Deployment:Invitations:0:ExpiresIn", "2.00:00:00");

        // When: The invitation is seeded, changed in the backing service, and seeding runs again.
        await _harness.SeedAsync();
        var created = _harness.Invitations[code];
        created.Status = InvitationStatus.Accepted;
        created.Email = "accepted@example.com";
        _harness.Set("IdentitySeeder:Deployment:Invitations:0:Email", "changed@example.com");
        await _harness.SeedAsync();

        // Then: The configured code and fields were used once, and existing lifecycle state and values were preserved.
        Assert.That(created.Code, Is.EqualTo(code));
        Assert.That(created.Status, Is.EqualTo(InvitationStatus.Accepted));
        Assert.That(created.Email, Is.EqualTo("accepted@example.com"));
        Assert.That(_harness.InvitationCreateCount, Is.EqualTo(1));
        Assert.That(_harness.LastInvitationRoles, Is.EqualTo(new[] { "Reader" }));
        Assert.That(_harness.LastInvitationClaims, Contains.Item(new ClaimInfo
        {
            Type = "department",
            Value = "engineering"
        }));
        Assert.That(_harness.LastInvitationExpiry, Is.EqualTo(TimeSpan.FromDays(2)));
        Assert.That(_harness.LastInvitationMetadata, Is.EqualTo("seeded"));
    }

    [TestCase(InvitationStatus.Pending)]
    [TestCase(InvitationStatus.Accepted)]
    [TestCase(InvitationStatus.Expired)]
    [TestCase(InvitationStatus.Revoked)]
    public async Task SeedAsync_LeavesExistingInvitationsInEveryLifecycleStateUnchanged(InvitationStatus status)
    {
        // Given: An existing invitation with a particular lifecycle state and differing configured fields.
        var code = Guid.NewGuid();
        var existing = _harness.AddExistingInvitation(code, status, "stored@example.com");
        _harness.Set("IdentitySeeder:Application:Invitations:0:Code", code.ToString("D"));
        _harness.Set("IdentitySeeder:Application:Invitations:0:Email", "configured@example.com");

        // When: Seeding finds the matching code.
        await _harness.SeedAsync();

        // Then: The existing state and values remain unchanged and create is not called.
        Assert.That(existing.Status, Is.EqualTo(status));
        Assert.That(existing.Email, Is.EqualTo("stored@example.com"));
        Assert.That(_harness.InvitationCreateCount, Is.Zero);
    }

    [Test]
    public async Task SeedAsync_IsolatesInvitationFailuresAndNeverLogsInvitationCode()
    {
        // Given: Invitation creation throws an exception containing its configured code, followed by a valid role.
        var code = Guid.NewGuid();
        _harness.ThrowOnInvitationCreate = true;
        _harness.Set("IdentitySeeder:Application:Invitations:0:Code", code.ToString("D"));
        _harness.Set("IdentitySeeder:Application:Roles:0:Name", "StillCreated");

        // When: Seeding runs.
        await _harness.SeedAsync();

        // Then: The role is still created, the run reports partial completion, and logs contain no invitation code.
        await using var scope = _harness.Services.CreateAsyncScope();
        Assert.That(await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>()
            .RoleExistsAsync("StillCreated"), Is.True);
        Assert.That(_harness.LogText, Does.Contain("partially completed"));
        Assert.That(_harness.LogText, Does.Not.Contain(code.ToString()));
    }

    [Test]
    public async Task SeedAsync_WithoutInvitationServiceWarnsOnceAndStillSeedsIdentity()
    {
        // Given: Invitation configuration exists, but no invitation service is registered.
        await _harness.DisposeAsync();
        _harness = await SeederHarness.CreateAsync(registerInvitationService: false);
        _harness.Set("IdentitySeeder:One:Invitations:0:Code", Guid.NewGuid().ToString("D"));
        _harness.Set("IdentitySeeder:Two:Invitations:0:Code", Guid.NewGuid().ToString("D"));
        _harness.Set("IdentitySeeder:Two:Roles:0:Name", "NoInvitationDependency");

        // When: Seeding runs.
        await _harness.SeedAsync();

        // Then: Both invitations are skipped with one warning, while role seeding succeeds.
        await using var scope = _harness.Services.CreateAsyncScope();
        Assert.That(await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>()
            .RoleExistsAsync("NoInvitationDependency"), Is.True);
        Assert.That(_harness.Logs.Count(log => log.Contains("IInvitationService is not registered")), Is.EqualTo(1));
        Assert.That(_harness.LogText, Does.Contain("skipped: 2"));
    }

    [Test]
    public async Task StartupExtension_UsesScopedSeederAndPropagatesCancellation()
    {
        // Given: The seeder is registered with a configured role and can be resolved separately in each scope.
        _harness.Set("IdentitySeeder:Application:Roles:0:Name", "StartupRole");
        await using var scopeOne = _harness.Services.CreateAsyncScope();
        await using var scopeTwo = _harness.Services.CreateAsyncScope();
        var first = scopeOne.ServiceProvider.GetRequiredService<IdentitySeeder<TestUser>>();
        var second = scopeTwo.ServiceProvider.GetRequiredService<IdentitySeeder<TestUser>>();

        // When: The application startup extension runs, then a canceled startup call is attempted.
        var app = new ApplicationBuilder(_harness.Services);
        AsyncDisposeProbeUserValidator.Reset();
        await app.SeedNuxtIdentityAsync<TestUser>();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Then: Startup seeding runs successfully in its async scope and cancellation is not swallowed.
        await using var verifyScope = _harness.Services.CreateAsyncScope();
        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(AsyncDisposeProbeUserValidator.DisposalCount, Is.EqualTo(1));
        Assert.That(await verifyScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>()
            .RoleExistsAsync("StartupRole"), Is.True);
        Assert.ThrowsAsync<OperationCanceledException>(
            () => app.SeedNuxtIdentityAsync<TestUser>(cancellation.Token));
    }

    [Test]
    public async Task StartupExtension_ThrowsClearlyWhenIdentityServicesAreNotRegistered()
    {
        // Given: Seeder registration exists but required Identity managers are absent.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddNuxtIdentitySeeding<TestUser>(configuration);
        await using var serviceProvider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(serviceProvider);

        // When: The startup extension attempts to resolve the seeder.
        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            () => app.SeedNuxtIdentityAsync<TestUser>());

        // Then: Dependency-injection setup errors propagate instead of being treated as item failures.
        Assert.That(exception!.Message, Does.Contain("UserManager`1"));
    }

    private sealed class SeederHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IConfigurationRoot _configuration;
        private readonly Mock<IInvitationService> _invitationService;
        private readonly Dictionary<Guid, InvitationEntity> _invitations = new();
        private readonly CapturingLoggerProvider _loggerProvider;
        private int _invitationCreateCount;
        private string[] _lastInvitationRoles = [];
        private IReadOnlyList<ClaimInfo> _lastInvitationClaims = Array.Empty<ClaimInfo>();
        private TimeSpan? _lastInvitationExpiry;
        private string? _lastInvitationMetadata;

        private SeederHarness(
            SqliteConnection connection,
            IConfigurationRoot configuration,
            ServiceProvider services,
            Mock<IInvitationService> invitationService,
            CapturingLoggerProvider loggerProvider)
        {
            _connection = connection;
            _configuration = configuration;
            Services = services;
            _invitationService = invitationService;
            _loggerProvider = loggerProvider;
        }

        public ServiceProvider Services { get; }
        public IReadOnlyDictionary<Guid, InvitationEntity> Invitations => _invitations;
        public IReadOnlyList<string> Logs => _loggerProvider.Messages;
        public string LogText => string.Join(Environment.NewLine, Logs);
        public int InvitationCreateCount => _invitationCreateCount;
        public string[] LastInvitationRoles => _lastInvitationRoles;
        public IReadOnlyList<ClaimInfo> LastInvitationClaims => _lastInvitationClaims;
        public TimeSpan? LastInvitationExpiry => _lastInvitationExpiry;
        public string? LastInvitationMetadata => _lastInvitationMetadata;
        public bool ThrowOnInvitationCreate { get; set; }

        public void Set(string key, string value)
        {
            _configuration[key] = value;
        }

        public InvitationEntity AddExistingInvitation(Guid code, InvitationStatus status, string email)
        {
            var invitation = new InvitationEntity
            {
                Code = code,
                Status = status,
                Email = email,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            };
            _invitations.Add(code, invitation);
            return invitation;
        }

        public async Task SeedAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IdentitySeeder<TestUser>>().SeedAsync();
        }

        public static async Task<SeederHarness> CreateAsync(bool registerInvitationService = true)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var invitationService = new Mock<IInvitationService>();
            var loggerProvider = new CapturingLoggerProvider();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddDbContext<TestDbContext>(options => options.UseSqlite(connection));
            services.AddIdentity<TestUser, IdentityRole>(options =>
                {
                    options.Password.RequireDigit = false;
                    options.Password.RequiredLength = 1;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequireUppercase = false;
                    options.Password.RequireLowercase = false;
                    options.User.RequireUniqueEmail = false;
                })
                .AddEntityFrameworkStores<TestDbContext>()
                .AddDefaultTokenProviders();
            services.AddScoped<IRoleValidator<IdentityRole>, RejectNamedRoleValidator>();
            services.AddScoped<IUserValidator<TestUser>, AsyncDisposeProbeUserValidator>();
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(loggerProvider);
                logging.AddFilter((category, level) =>
                    category is not null &&
                    category.StartsWith("NuxtIdentity.AspNetCore.Services.IdentitySeeder", StringComparison.Ordinal) &&
                    level >= LogLevel.Debug);
            });
            services.AddNuxtIdentitySeeding<TestUser>(configuration);

            if (registerInvitationService)
                services.AddSingleton(invitationService.Object);

            var harness = new SeederHarness(
                connection,
                configuration,
                services.BuildServiceProvider(),
                invitationService,
                loggerProvider);

            if (registerInvitationService)
            {
                invitationService
                    .Setup(service => service.GetByCodeAsync(It.IsAny<string>()))
                    .ReturnsAsync((string code) =>
                    {
                        return Guid.TryParse(code, out var guid) && harness._invitations.TryGetValue(guid, out var invitation)
                            ? invitation
                            : null;
                    });
                invitationService
                    .Setup(service => service.CreateAsync(
                        It.IsAny<string?>(),
                        It.IsAny<IReadOnlyList<string>?>(),
                        It.IsAny<IReadOnlyList<ClaimInfo>?>(),
                        It.IsAny<TimeSpan?>(),
                        It.IsAny<string?>(),
                        It.IsAny<Guid?>()))
                    .ReturnsAsync((
                        string? email,
                        IReadOnlyList<string>? roles,
                        IReadOnlyList<ClaimInfo>? claims,
                        TimeSpan? expiresIn,
                        string? metadata,
                        Guid? code) =>
                    {
                        if (harness.ThrowOnInvitationCreate)
                            throw new InvalidOperationException($"Invitation code {code} failed.");

                        var invitation = new InvitationEntity
                        {
                            Code = code!.Value,
                            Email = email,
                            Status = InvitationStatus.Pending,
                            Roles = roles is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(roles) : null,
                            Claims = claims is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(claims) : null,
                            Metadata = metadata,
                            ExpiresAt = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromDays(30))
                        };
                        harness._invitations[code.Value] = invitation;
                        harness._invitationCreateCount++;
                        harness._lastInvitationRoles = roles?.ToArray() ?? [];
                        harness._lastInvitationClaims = claims ?? Array.Empty<ClaimInfo>();
                        harness._lastInvitationExpiry = expiresIn;
                        harness._lastInvitationMetadata = metadata;
                        return invitation;
                    });
            }

            await using var scope = harness.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreatedAsync();
            return harness;
        }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            await _connection.DisposeAsync();
            _loggerProvider.Dispose();
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyList<string> Messages => _messages.ToArray();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Enqueue(formatter(state, exception));
        }
    }

    private sealed class RejectNamedRoleValidator : IRoleValidator<IdentityRole>
    {
        public Task<IdentityResult> ValidateAsync(
            RoleManager<IdentityRole> manager,
            IdentityRole role)
        {
            return Task.FromResult(role.Name == "RejectedRole"
                ? IdentityResult.Failed(new IdentityError { Description = "Rejected for test." })
                : IdentityResult.Success);
        }
    }

    private sealed class AsyncDisposeProbeUserValidator : IUserValidator<TestUser>, IAsyncDisposable
    {
        private static int _disposalCount;

        public static int DisposalCount => Volatile.Read(ref _disposalCount);

        public static void Reset()
        {
            Interlocked.Exchange(ref _disposalCount, 0);
        }

        public Task<IdentityResult> ValidateAsync(UserManager<TestUser> manager, TestUser user)
        {
            return Task.FromResult(IdentityResult.Success);
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposalCount);
            return ValueTask.CompletedTask;
        }
    }
}

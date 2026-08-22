using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NuxtIdentity.Core.Models;
using NuxtIdentity.EntityFrameworkCore.Extensions;
using NuxtIdentity.EntityFrameworkCore.Services;
using NuxtIdentity.EntityFrameworkCore.Tests.Helpers;

namespace NuxtIdentity.EntityFrameworkCore.Tests.Services;

/// <summary>
/// Integration tests for <see cref="EfInvitationService{TContext}"/>.
/// </summary>
[TestFixture]
[Category("Integration")]
public class EfInvitationServiceTests
{
    private TestDbContext _context = null!;
    private FakeTimeProvider _timeProvider = null!;
    private EfInvitationService<TestDbContext> _service = null!;

    [SetUp]
    public void SetUp()
    {
        // Given: An in-memory database for testing
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new TestDbContext(options);

        // And: A fake time provider initialized to current time for deterministic control
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        // And: A logger
        var loggerMock = new Mock<ILogger<EfInvitationService<TestDbContext>>>();

        _service = new EfInvitationService<TestDbContext>(
            _context,
            loggerMock.Object,
            _timeProvider);
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
    }

    #region CreateAsync

    [Test]
    public async Task CreateAsync_ValidData_ReturnsEntityWithGeneratedCode()
    {
        // Given: Valid invitation data
        var email = "user@test.com";
        var roles = new List<string> { "Admin" };
        var claims = new List<ClaimInfo> { new() { Type = "department", Value = "engineering" } };
        var expiresIn = TimeSpan.FromDays(7);

        // When: Creating an invitation
        var entity = await _service.CreateAsync(email, roles, claims, expiresIn);

        // Then: The entity should have a non-empty Code
        entity.Code.Should().NotBe(Guid.Empty);

        // And: The email should match
        entity.Email.Should().Be(email);
    }

    [Test]
    public async Task CreateAsync_ValidData_PersistsToDatabase()
    {
        // Given: Valid invitation data
        var email = "user@test.com";
        var roles = new List<string> { "Admin" };
        var claims = new List<ClaimInfo> { new() { Type = "department", Value = "engineering" } };
        var expiresIn = TimeSpan.FromDays(7);

        // When: Creating an invitation
        await _service.CreateAsync(email, roles, claims, expiresIn);

        // Then: The invitation should be stored in the database
        var count = await _context.Invitations.CountAsync();
        count.Should().Be(1);
    }

    [Test]
    public async Task CreateAsync_ValidData_StatusIsPending()
    {
        // Given: Valid invitation data
        var email = "user@test.com";
        var roles = new List<string>();
        var claims = new List<ClaimInfo>();
        var expiresIn = TimeSpan.FromDays(7);

        // When: Creating an invitation
        var entity = await _service.CreateAsync(email, roles, claims, expiresIn);

        // Then: The status should be Pending
        entity.Status.Should().Be(InvitationStatus.Pending);
    }

    [Test]
    public async Task CreateAsync_WithRolesAndClaims_SerializesAsJson()
    {
        // Given: Invitation data with roles and claims
        var email = "user@test.com";
        var roles = new List<string> { "Admin", "User" };
        var claims = new List<ClaimInfo>
        {
            new() { Type = "department", Value = "engineering" },
            new() { Type = "level", Value = "senior" }
        };
        var expiresIn = TimeSpan.FromDays(7);

        // When: Creating an invitation
        var entity = await _service.CreateAsync(email, roles, claims, expiresIn);

        // Then: Roles should be JSON-serialized
        entity.Roles.Should().NotBeNull();
        entity.Roles.Should().Contain("Admin");
        entity.Roles.Should().Contain("User");

        // And: Claims should be JSON-serialized
        entity.Claims.Should().NotBeNull();
        entity.Claims.Should().Contain("department");
        entity.Claims.Should().Contain("engineering");
    }

    [Test]
    public async Task CreateAsync_WithEmptyRolesAndClaims_StoresNull()
    {
        // Given: Invitation data with empty roles and claims
        var email = "user@test.com";
        var roles = new List<string>();
        var claims = new List<ClaimInfo>();
        var expiresIn = TimeSpan.FromDays(7);

        // When: Creating an invitation
        var entity = await _service.CreateAsync(email, roles, claims, expiresIn);

        // Then: Roles and Claims should be null
        entity.Roles.Should().BeNull();
        entity.Claims.Should().BeNull();
    }

    [Test]
    public async Task CreateAsync_WithMetadata_StoresMetadata()
    {
        // Given: Invitation data with metadata
        var email = "user@test.com";
        var roles = new List<string>();
        var claims = new List<ClaimInfo>();
        var expiresIn = TimeSpan.FromDays(7);
        var metadata = """{"source": "admin-portal"}""";

        // When: Creating an invitation
        var entity = await _service.CreateAsync(email, roles, claims, expiresIn, metadata);

        // Then: Metadata should be stored
        entity.Metadata.Should().Be(metadata);
    }

    [Test]
    public async Task CreateAsync_ValidData_CalculatesExpirationCorrectly()
    {
        // Given: A known current time and expiration duration
        var currentTime = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresIn = TimeSpan.FromDays(7);

        // When: Creating an invitation
        var entity = await _service.CreateAsync("user@test.com", new List<string>(),
            new List<ClaimInfo>(), expiresIn);

        // Then: ExpiresAt should be current time + expiration duration
        entity.ExpiresAt.Should().Be(currentTime.Add(expiresIn));

        // And: CreatedAt should be the current time
        entity.CreatedAt.Should().Be(currentTime);
    }

    [Test]
    public async Task CreateAsync_AlwaysCreatesPending()
    {
        // Given: Valid invitation data

        // When: Creating an invitation
        var entity = await _service.CreateAsync("user@test.com", new List<string>(),
            new List<ClaimInfo>(), TimeSpan.FromDays(7));

        // Then: The status should always be Pending
        entity.Status.Should().Be(InvitationStatus.Pending);
    }

    [Test]
    public async Task CreateAsync_NoParameters_UsesDefaults()
    {
        // Given: No parameters specified

        // When: Creating an invitation with all defaults
        var entity = await _service.CreateAsync();

        // Then: The entity should have a non-empty Code
        entity.Code.Should().NotBe(Guid.Empty);

        // And: Email should be null
        entity.Email.Should().BeNull();

        // And: Status should be Pending
        entity.Status.Should().Be(InvitationStatus.Pending);

        // And: Roles and Claims should be null (empty collections stored as null)
        entity.Roles.Should().BeNull();
        entity.Claims.Should().BeNull();

        // And: Metadata should be null
        entity.Metadata.Should().BeNull();

        // And: ExpiresAt should be 30 days from creation
        var expectedExpiry = entity.CreatedAt.AddDays(30);
        entity.ExpiresAt.Should().Be(expectedExpiry);
    }

    [Test]
    public async Task CreateAsync_NullRolesAndClaims_StoresNull()
    {
        // Given: Null roles and claims (explicitly passed)

        // When: Creating an invitation with null roles and claims
        var entity = await _service.CreateAsync(
            email: "user@test.com",
            roles: null,
            claims: null,
            expiresIn: TimeSpan.FromDays(7));

        // Then: Roles should be null
        entity.Roles.Should().BeNull();

        // And: Claims should be null
        entity.Claims.Should().BeNull();
    }

    [Test]
    public async Task CreateAsync_NullEmail_StoresNull()
    {
        // Given: No email specified

        // When: Creating an invitation without an email
        var entity = await _service.CreateAsync(
            roles: new List<string> { "Admin" },
            claims: new List<ClaimInfo>(),
            expiresIn: TimeSpan.FromDays(7));

        // Then: Email should be null
        entity.Email.Should().BeNull();

        // And: The invitation should be persisted
        var stored = await _context.Invitations.FindAsync(entity.Id);
        stored.Should().NotBeNull();
        stored!.Email.Should().BeNull();
    }

    [Test]
    public async Task CreateAsync_WithSpecificCode_UsesProvidedCode()
    {
        // Given: A specific code to use for the invitation
        var specificCode = Guid.NewGuid();

        // When: Creating an invitation with that code
        var entity = await _service.CreateAsync(email: "user@test.com", code: specificCode);

        // Then: The entity should have exactly the provided code
        entity.Code.Should().Be(specificCode);

        // And: The invitation should be retrievable by that code
        var stored = await _service.GetByCodeAsync(specificCode.ToString());
        stored.Should().NotBeNull();
        stored!.Id.Should().Be(entity.Id);
    }

    [Test]
    public async Task CreateAsync_NullExpiresIn_DefaultsTo30Days()
    {
        // Given: No expiration specified
        var currentTime = _timeProvider.GetUtcNow().UtcDateTime;

        // When: Creating an invitation without an expiration
        var entity = await _service.CreateAsync(email: "user@test.com");

        // Then: ExpiresAt should be 30 days from creation
        entity.ExpiresAt.Should().Be(currentTime.AddDays(30));
    }

    #endregion

    #region GetByCodeAsync

    [Test]
    public async Task GetByCodeAsync_ExistingCode_ReturnsEntity()
    {
        // Given: An invitation exists in the database
        var created = await _service.CreateAsync("user@test.com", new List<string>(),
            new List<ClaimInfo>(), TimeSpan.FromDays(7));

        // When: Looking up by its code
        var entity = await _service.GetByCodeAsync(created.Code.ToString());

        // Then: The entity should be returned
        entity.Should().NotBeNull();
        entity!.Id.Should().Be(created.Id);
        entity.Email.Should().Be("user@test.com");
    }

    [Test]
    public async Task GetByCodeAsync_NonexistentCode_ReturnsNull()
    {
        // Given: No invitations exist in the database

        // When: Looking up a non-existent code
        var entity = await _service.GetByCodeAsync(Guid.NewGuid().ToString());

        // Then: Null should be returned
        entity.Should().BeNull();
    }

    [Test]
    public async Task GetByCodeAsync_InvalidGuidFormat_ReturnsNull()
    {
        // Given: An invalid GUID string

        // When: Looking up with an invalid format
        var entity = await _service.GetByCodeAsync("not-a-guid");

        // Then: Null should be returned
        entity.Should().BeNull();
    }

    #endregion

    #region ResolveStatusAsync

    [Test]
    public async Task ResolveStatusAsync_PendingInvitation_ReturnsPending()
    {
        // Given: A pending invitation
        var created = await _service.CreateAsync("user@test.com", new List<string>(),
            new List<ClaimInfo>(), TimeSpan.FromDays(7));

        // When: Resolving the status
        var status = await _service.ResolveStatusAsync(created.Code.ToString());

        // Then: Status should be Pending
        status.Should().Be(InvitationStatus.Pending);
    }

    [Test]
    public async Task ResolveStatusAsync_AcceptedInvitation_ReturnsAccepted()
    {
        // Given: An invitation that has been accepted
        var created = await _service.CreateAsync("user@test.com", new List<string>(),
            new List<ClaimInfo>(), TimeSpan.FromDays(7));
        await _service.AcceptAsync(created, "user-123");

        // When: Resolving the status
        var status = await _service.ResolveStatusAsync(created.Code.ToString());

        // Then: Status should be Accepted
        status.Should().Be(InvitationStatus.Accepted);
    }

    [Test]
    public async Task ResolveStatusAsync_RevokedInvitation_ReturnsRevoked()
    {
        // Given: A revoked invitation (created via test method)
        var created = await _service.CreateTestAsync(new InvitationEntity
        {
            Email = "user@test.com",
            Status = InvitationStatus.Revoked
        });

        // When: Resolving the status
        var status = await _service.ResolveStatusAsync(created.Code.ToString());

        // Then: Status should be Revoked
        status.Should().Be(InvitationStatus.Revoked);
    }

    [Test]
    public async Task ResolveStatusAsync_ExpiredInvitation_ReturnsExpired()
    {
        // Given: A pending invitation
        var created = await _service.CreateAsync("user@test.com", new List<string>(),
            new List<ClaimInfo>(), TimeSpan.FromDays(7));

        // When: Time passes beyond the expiration
        _timeProvider.Advance(TimeSpan.FromDays(8));

        // And: Resolving the status
        var status = await _service.ResolveStatusAsync(created.Code.ToString());

        // Then: Status should be Expired
        status.Should().Be(InvitationStatus.Expired);
    }

    [Test]
    public async Task ResolveStatusAsync_UnknownCode_ReturnsNotFound()
    {
        // Given: No invitations exist

        // When: Resolving status for an unknown code
        var status = await _service.ResolveStatusAsync(Guid.NewGuid().ToString());

        // Then: Status should be NotFound
        status.Should().Be(InvitationStatus.NotFound);
    }

    #endregion

    #region ValidateAsync

    [Test]
    public async Task ValidateAsync_PendingAndNotExpired_ReturnsEntity()
    {
        // Given: A pending invitation that has not expired
        var created = await _service.CreateAsync("user@test.com", new List<string>(),
            new List<ClaimInfo>(), TimeSpan.FromDays(7));

        // When: Validating the invitation
        var entity = await _service.ValidateAsync(created.Code.ToString());

        // Then: The entity should be returned
        entity.Should().NotBeNull();
        entity!.Id.Should().Be(created.Id);
    }

    [Test]
    public async Task ValidateAsync_ExpiredInvitation_ReturnsNull()
    {
        // Given: A pending invitation
        var created = await _service.CreateAsync("user@test.com", new List<string>(),
            new List<ClaimInfo>(), TimeSpan.FromDays(7));

        // When: Time passes beyond the expiration
        _timeProvider.Advance(TimeSpan.FromDays(8));

        // And: Validating the invitation
        var entity = await _service.ValidateAsync(created.Code.ToString());

        // Then: Null should be returned
        entity.Should().BeNull();
    }

    [Test]
    public async Task ValidateAsync_AcceptedInvitation_ReturnsNull()
    {
        // Given: An accepted invitation
        var created = await _service.CreateAsync("user@test.com", new List<string>(),
            new List<ClaimInfo>(), TimeSpan.FromDays(7));
        await _service.AcceptAsync(created, "user-123");

        // When: Validating the invitation
        var entity = await _service.ValidateAsync(created.Code.ToString());

        // Then: Null should be returned
        entity.Should().BeNull();
    }

    [Test]
    public async Task ValidateAsync_RevokedInvitation_ReturnsNull()
    {
        // Given: A revoked invitation (created via test method)
        var created = await _service.CreateTestAsync(new InvitationEntity
        {
            Email = "user@test.com",
            Status = InvitationStatus.Revoked
        });

        // When: Validating the invitation
        var entity = await _service.ValidateAsync(created.Code.ToString());

        // Then: Null should be returned
        entity.Should().BeNull();
    }

    [Test]
    public async Task ValidateAsync_UnknownCode_ReturnsNull()
    {
        // Given: No invitations exist

        // When: Validating an unknown code
        var entity = await _service.ValidateAsync(Guid.NewGuid().ToString());

        // Then: Null should be returned
        entity.Should().BeNull();
    }

    #endregion

    #region AcceptAsync

    [Test]
    public async Task AcceptAsync_PendingInvitation_SetsAcceptedStatus()
    {
        // Given: A pending invitation
        var created = await _service.CreateAsync("user@test.com", new List<string>(),
            new List<ClaimInfo>(), TimeSpan.FromDays(7));

        // When: Accepting the invitation
        await _service.AcceptAsync(created, "user-123");

        // Then: The status should be Accepted
        var stored = await _context.Invitations.FindAsync(created.Id);
        stored!.Status.Should().Be(InvitationStatus.Accepted);
    }

    [Test]
    public async Task AcceptAsync_PendingInvitation_SetsAcceptedAtAndUserId()
    {
        // Given: A pending invitation
        var created = await _service.CreateAsync("user@test.com", new List<string>(),
            new List<ClaimInfo>(), TimeSpan.FromDays(7));
        var currentTime = _timeProvider.GetUtcNow().UtcDateTime;

        // When: Accepting the invitation
        await _service.AcceptAsync(created, "user-123");

        // Then: AcceptedAt should be set to current time
        var stored = await _context.Invitations.FindAsync(created.Id);
        stored!.AcceptedAt.Should().Be(currentTime);

        // And: AcceptedByUserId should be set
        stored.AcceptedByUserId.Should().Be("user-123");
    }

    #endregion

    #region CreateTestAsync

    [Test]
    public async Task CreateTestAsync_WithAllProperties_PersistsCorrectly()
    {
        // Given: A fully-specified test invitation
        var code = Guid.NewGuid();
        var invitation = new InvitationEntity
        {
            Code = code,
            Email = "test@test.com",
            Status = InvitationStatus.Accepted,
            Roles = """["Admin"]""",
            Claims = """[{"Type":"dept","Value":"eng"}]""",
            Metadata = """{"key":"value"}""",
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            AcceptedAt = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            AcceptedByUserId = "user-456"
        };

        // When: Creating a test invitation
        var result = await _service.CreateTestAsync(invitation);

        // Then: All caller-set properties should be persisted
        result.Code.Should().Be(code);
        result.Email.Should().Be("test@test.com");
        result.Status.Should().Be(InvitationStatus.Accepted);
        result.Roles.Should().Be("""["Admin"]""");
        result.Claims.Should().Be("""[{"Type":"dept","Value":"eng"}]""");
        result.Metadata.Should().Be("""{"key":"value"}""");
        result.CreatedAt.Should().Be(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        result.ExpiresAt.Should().Be(new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        result.AcceptedAt.Should().Be(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        result.AcceptedByUserId.Should().Be("user-456");

        // And: IsTest should be true
        result.IsTest.Should().BeTrue();

        // And: Id should be auto-generated (non-zero)
        result.Id.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task CreateTestAsync_ForcesIsTestTrue()
    {
        // Given: A test invitation with IsTest explicitly set to false
        var invitation = new InvitationEntity
        {
            Email = "test@test.com",
            IsTest = false
        };

        // When: Creating a test invitation
        var result = await _service.CreateTestAsync(invitation);

        // Then: IsTest should be true regardless of caller's value
        result.IsTest.Should().BeTrue();
    }

    [Test]
    public async Task CreateTestAsync_WithEmptyCode_GeneratesNewCode()
    {
        // Given: A test invitation with no code (Guid.Empty)
        var invitation = new InvitationEntity
        {
            Email = "test@test.com",
            Code = Guid.Empty
        };

        // When: Creating a test invitation
        var result = await _service.CreateTestAsync(invitation);

        // Then: A non-empty code should be generated
        result.Code.Should().NotBe(Guid.Empty);
    }

    [Test]
    public async Task CreateTestAsync_WithDefaultTimestamps_AppliesDefaults()
    {
        // Given: A test invitation with default timestamps
        var currentTime = _timeProvider.GetUtcNow().UtcDateTime;
        var invitation = new InvitationEntity
        {
            Email = "test@test.com"
        };

        // When: Creating a test invitation
        var result = await _service.CreateTestAsync(invitation);

        // Then: CreatedAt should be current time
        result.CreatedAt.Should().Be(currentTime);

        // And: ExpiresAt should be 15 minutes from now (DefaultTestExpiration)
        result.ExpiresAt.Should().Be(currentTime.AddMinutes(15));
    }

    [Test]
    public async Task CreateTestAsync_IgnoresId()
    {
        // Given: A test invitation with an explicit Id
        var invitation = new InvitationEntity
        {
            Id = 999,
            Email = "test@test.com"
        };

        // When: Creating a test invitation
        var result = await _service.CreateTestAsync(invitation);

        // Then: Id should be auto-generated, not the caller's value
        result.Id.Should().NotBe(999);
        result.Id.Should().BeGreaterThan(0);
    }

    [Test]
    public void CreateTestAsync_NullEmail_ThrowsArgumentException()
    {
        // Given: A test invitation with null email
        var invitation = new InvitationEntity
        {
            Email = null
        };

        // When: Creating a test invitation
        // Then: ArgumentException should be thrown
        var act = () => _service.CreateTestAsync(invitation);
        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Email*required*");
    }

    [Test]
    public void CreateTestAsync_EmptyEmail_ThrowsArgumentException()
    {
        // Given: A test invitation with empty email
        var invitation = new InvitationEntity
        {
            Email = ""
        };

        // When: Creating a test invitation
        // Then: ArgumentException should be thrown
        var act = () => _service.CreateTestAsync(invitation);
        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Email*required*");
    }

    [Test]
    public void CreateTestAsync_NullInvitation_ThrowsArgumentNullException()
    {
        // Given: A null invitation

        // When: Creating a test invitation
        // Then: ArgumentNullException should be thrown
        var act = () => _service.CreateTestAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task CreateTestAsync_CodeIsAvailableOnPassedInObject()
    {
        // Given: A test invitation with empty code
        var invitation = new InvitationEntity
        {
            Email = "test@test.com",
            Code = Guid.Empty
        };

        // When: Creating a test invitation
        await _service.CreateTestAsync(invitation);

        // Then: The generated code should be available on the original object
        invitation.Code.Should().NotBe(Guid.Empty);
    }

    #endregion

    #region DeleteTestInvitationsAsync

    [Test]
    public async Task DeleteTestInvitationsAsync_DeletesTestInvitations()
    {
        // Given: Two test invitations and one production invitation
        await _service.CreateTestAsync(new InvitationEntity { Email = "test1@test.com" });
        await _service.CreateTestAsync(new InvitationEntity { Email = "test2@test.com" });
        await _service.CreateAsync("prod@test.com");

        // When: Deleting test invitations
        var count = await _service.DeleteTestInvitationsAsync();

        // Then: Two invitations should be deleted
        count.Should().Be(2);

        // And: Only the production invitation should remain
        var remaining = await _context.Invitations.ToListAsync();
        remaining.Should().HaveCount(1);
        remaining[0].Email.Should().Be("prod@test.com");
        remaining[0].IsTest.Should().BeFalse();
    }

    [Test]
    public async Task DeleteTestInvitationsAsync_NoTestInvitations_ReturnsZero()
    {
        // Given: Only production invitations exist
        await _service.CreateAsync("prod@test.com");

        // When: Deleting test invitations
        var count = await _service.DeleteTestInvitationsAsync();

        // Then: Zero should be returned
        count.Should().Be(0);

        // And: The production invitation should still exist
        var remaining = await _context.Invitations.CountAsync();
        remaining.Should().Be(1);
    }

    [Test]
    public async Task DeleteTestInvitationsAsync_EmptyDatabase_ReturnsZero()
    {
        // Given: No invitations exist

        // When: Deleting test invitations
        var count = await _service.DeleteTestInvitationsAsync();

        // Then: Zero should be returned
        count.Should().Be(0);
    }

    #endregion
}

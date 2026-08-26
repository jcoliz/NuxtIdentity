using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NuxtIdentity.Core.Abstractions;
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
    private IInvitationService _service = null!;

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
        Assert.That(entity.Code, Is.Not.EqualTo(Guid.Empty));

        // And: The email should match
        Assert.That(entity.Email, Is.EqualTo(email));
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
        Assert.That(count, Is.EqualTo(1));
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
        Assert.That(entity.Status, Is.EqualTo(InvitationStatus.Pending));
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
        Assert.That(entity.Roles, Is.Not.Null);
        Assert.That(entity.Roles, Does.Contain("Admin"));
        Assert.That(entity.Roles, Does.Contain("User"));

        // And: Claims should be JSON-serialized
        Assert.That(entity.Claims, Is.Not.Null);
        Assert.That(entity.Claims, Does.Contain("department"));
        Assert.That(entity.Claims, Does.Contain("engineering"));
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
        Assert.That(entity.Roles, Is.Null);
        Assert.That(entity.Claims, Is.Null);
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
        Assert.That(entity.Metadata, Is.EqualTo(metadata));
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
        Assert.That(entity.ExpiresAt, Is.EqualTo(currentTime.Add(expiresIn)));

        // And: CreatedAt should be the current time
        Assert.That(entity.CreatedAt, Is.EqualTo(currentTime));
    }

    [Test]
    public async Task CreateAsync_AlwaysCreatesPending()
    {
        // Given: Valid invitation data

        // When: Creating an invitation
        var entity = await _service.CreateAsync("user@test.com", new List<string>(),
            new List<ClaimInfo>(), TimeSpan.FromDays(7));

        // Then: The status should always be Pending
        Assert.That(entity.Status, Is.EqualTo(InvitationStatus.Pending));
    }

    [Test]
    public async Task CreateAsync_NoParameters_UsesDefaults()
    {
        // Given: No parameters specified

        // When: Creating an invitation with all defaults
        var entity = await _service.CreateAsync();

        // Then: The entity should have a non-empty Code
        Assert.That(entity.Code, Is.Not.EqualTo(Guid.Empty));

        // And: Email should be null
        Assert.That(entity.Email, Is.Null);

        // And: Status should be Pending
        Assert.That(entity.Status, Is.EqualTo(InvitationStatus.Pending));

        // And: Roles and Claims should be null (empty collections stored as null)
        Assert.That(entity.Roles, Is.Null);
        Assert.That(entity.Claims, Is.Null);

        // And: Metadata should be null
        Assert.That(entity.Metadata, Is.Null);

        // And: ExpiresAt should be 30 days from creation
        var expectedExpiry = entity.CreatedAt.AddDays(30);
        Assert.That(entity.ExpiresAt, Is.EqualTo(expectedExpiry));
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
        Assert.That(entity.Roles, Is.Null);

        // And: Claims should be null
        Assert.That(entity.Claims, Is.Null);
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
        Assert.That(entity.Email, Is.Null);

        // And: The invitation should be persisted
        var stored = await _context.Invitations.FindAsync(entity.Id);
        Assert.That(stored, Is.Not.Null);
        Assert.That(stored!.Email, Is.Null);
    }

    [Test]
    public async Task CreateAsync_WithSpecificCode_UsesProvidedCode()
    {
        // Given: A specific code to use for the invitation
        var specificCode = Guid.NewGuid();

        // When: Creating an invitation with that code
        var entity = await _service.CreateAsync(email: "user@test.com", code: specificCode);

        // Then: The entity should have exactly the provided code
        Assert.That(entity.Code, Is.EqualTo(specificCode));

        // And: The invitation should be retrievable by that code
        var stored = await _service.GetByCodeAsync(specificCode.ToString());
        Assert.That(stored, Is.Not.Null);
        Assert.That(stored!.Id, Is.EqualTo(entity.Id));
    }

    [Test]
    public async Task CreateAsync_NullExpiresIn_DefaultsTo30Days()
    {
        // Given: No expiration specified
        var currentTime = _timeProvider.GetUtcNow().UtcDateTime;

        // When: Creating an invitation without an expiration
        var entity = await _service.CreateAsync(email: "user@test.com");

        // Then: ExpiresAt should be 30 days from creation
        Assert.That(entity.ExpiresAt, Is.EqualTo(currentTime.AddDays(30)));
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
        Assert.That(entity, Is.Not.Null);
        Assert.That(entity!.Id, Is.EqualTo(created.Id));
        Assert.That(entity.Email, Is.EqualTo("user@test.com"));
    }

    [Test]
    public async Task GetByCodeAsync_NonexistentCode_ReturnsNull()
    {
        // Given: No invitations exist in the database

        // When: Looking up a non-existent code
        var entity = await _service.GetByCodeAsync(Guid.NewGuid().ToString());

        // Then: Null should be returned
        Assert.That(entity, Is.Null);
    }

    [Test]
    public async Task GetByCodeAsync_InvalidGuidFormat_ReturnsNull()
    {
        // Given: An invalid GUID string

        // When: Looking up with an invalid format
        var entity = await _service.GetByCodeAsync("not-a-guid");

        // Then: Null should be returned
        Assert.That(entity, Is.Null);
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
        Assert.That(status, Is.EqualTo(InvitationStatus.Pending));
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
        Assert.That(status, Is.EqualTo(InvitationStatus.Accepted));
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
        Assert.That(status, Is.EqualTo(InvitationStatus.Revoked));
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
        Assert.That(status, Is.EqualTo(InvitationStatus.Expired));
    }

    [Test]
    public async Task ResolveStatusAsync_UnknownCode_ReturnsNotFound()
    {
        // Given: No invitations exist

        // When: Resolving status for an unknown code
        var status = await _service.ResolveStatusAsync(Guid.NewGuid().ToString());

        // Then: Status should be NotFound
        Assert.That(status, Is.EqualTo(InvitationStatus.NotFound));
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
        Assert.That(entity, Is.Not.Null);
        Assert.That(entity!.Id, Is.EqualTo(created.Id));
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
        Assert.That(entity, Is.Null);
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
        Assert.That(entity, Is.Null);
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
        Assert.That(entity, Is.Null);
    }

    [Test]
    public async Task ValidateAsync_UnknownCode_ReturnsNull()
    {
        // Given: No invitations exist

        // When: Validating an unknown code
        var entity = await _service.ValidateAsync(Guid.NewGuid().ToString());

        // Then: Null should be returned
        Assert.That(entity, Is.Null);
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
        Assert.That(stored!.Status, Is.EqualTo(InvitationStatus.Accepted));
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
        Assert.That(stored!.AcceptedAt, Is.EqualTo(currentTime));

        // And: AcceptedByUserId should be set
        Assert.That(stored.AcceptedByUserId, Is.EqualTo("user-123"));
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
        Assert.That(result.Code, Is.EqualTo(code));
        Assert.That(result.Email, Is.EqualTo("test@test.com"));
        Assert.That(result.Status, Is.EqualTo(InvitationStatus.Accepted));
        Assert.That(result.Roles, Is.EqualTo("""["Admin"]"""));
        Assert.That(result.Claims, Is.EqualTo("""[{"Type":"dept","Value":"eng"}]"""));
        Assert.That(result.Metadata, Is.EqualTo("""{"key":"value"}"""));
        Assert.That(result.CreatedAt, Is.EqualTo(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.That(result.ExpiresAt, Is.EqualTo(new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.That(result.AcceptedAt, Is.EqualTo(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc)));
        Assert.That(result.AcceptedByUserId, Is.EqualTo("user-456"));

        // And: IsTest should be true
        Assert.That(result.IsTest, Is.True);

        // And: Id should be auto-generated (non-zero)
        Assert.That(result.Id, Is.GreaterThan(0));
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
        Assert.That(result.IsTest, Is.True);
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
        Assert.That(result.Code, Is.Not.EqualTo(Guid.Empty));
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
        Assert.That(result.CreatedAt, Is.EqualTo(currentTime));

        // And: ExpiresAt should be 15 minutes from now (DefaultTestExpiration)
        Assert.That(result.ExpiresAt, Is.EqualTo(currentTime.AddMinutes(15)));
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
        Assert.That(result.Id, Is.Not.EqualTo(999));
        Assert.That(result.Id, Is.GreaterThan(0));
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
        var ex = Assert.ThrowsAsync<ArgumentException>(async () => await _service.CreateTestAsync(invitation));
        Assert.That(ex!.Message, Does.Match("Email.*required"));
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
        var ex = Assert.ThrowsAsync<ArgumentException>(async () => await _service.CreateTestAsync(invitation));
        Assert.That(ex!.Message, Does.Match("Email.*required"));
    }

    [Test]
    public void CreateTestAsync_NullInvitation_ThrowsArgumentNullException()
    {
        // Given: A null invitation

        // When: Creating a test invitation
        // Then: ArgumentNullException should be thrown
        Assert.ThrowsAsync<ArgumentNullException>(async () => await _service.CreateTestAsync(null!));
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
        Assert.That(invitation.Code, Is.Not.EqualTo(Guid.Empty));
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
        Assert.That(count, Is.EqualTo(2));

        // And: Only the production invitation should remain
        var remaining = await _context.Invitations.ToListAsync();
        Assert.That(remaining, Has.Count.EqualTo(1));
        Assert.That(remaining[0].Email, Is.EqualTo("prod@test.com"));
        Assert.That(remaining[0].IsTest, Is.False);
    }

    [Test]
    public async Task DeleteTestInvitationsAsync_NoTestInvitations_ReturnsZero()
    {
        // Given: Only production invitations exist
        await _service.CreateAsync("prod@test.com");

        // When: Deleting test invitations
        var count = await _service.DeleteTestInvitationsAsync();

        // Then: Zero should be returned
        Assert.That(count, Is.EqualTo(0));

        // And: The production invitation should still exist
        var remaining = await _context.Invitations.CountAsync();
        Assert.That(remaining, Is.EqualTo(1));
    }

    [Test]
    public async Task DeleteTestInvitationsAsync_EmptyDatabase_ReturnsZero()
    {
        // Given: No invitations exist

        // When: Deleting test invitations
        var count = await _service.DeleteTestInvitationsAsync();

        // Then: Zero should be returned
        Assert.That(count, Is.EqualTo(0));
    }

    #endregion

    #region ListAsync

    [Test]
    [Explicit("ListAsync is not yet implemented in EfInvitationService.")]
    public async Task ListAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Given: No invitations exist in the database

        // When: Listing invitations with default parameters
        var result = await _service.ListAsync();

        // Then: An empty list should be returned
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    [Explicit("ListAsync is not yet implemented in EfInvitationService.")]
    public async Task ListAsync_MultipleInvitations_ReturnsAll()
    {
        // Given: Three invitations exist in the database
        await _service.CreateAsync("user1@test.com");
        await _service.CreateAsync("user2@test.com");
        await _service.CreateAsync("user3@test.com");

        // When: Listing invitations with default parameters
        var result = await _service.ListAsync();

        // Then: All three invitations should be returned
        Assert.That(result, Has.Count.EqualTo(3));
    }

    [Test]
    [Explicit("ListAsync is not yet implemented in EfInvitationService.")]
    public async Task ListAsync_WithOffset_SkipsFirstItems()
    {
        // Given: Three invitations exist in the database
        await _service.CreateAsync("user1@test.com");
        await _service.CreateAsync("user2@test.com");
        await _service.CreateAsync("user3@test.com");

        // When: Listing with offset of 1
        var result = await _service.ListAsync(offset: 1);

        // Then: Two invitations should be returned (skipping the first)
        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    [Explicit("ListAsync is not yet implemented in EfInvitationService.")]
    public async Task ListAsync_WithCount_LimitsResults()
    {
        // Given: Three invitations exist in the database
        await _service.CreateAsync("user1@test.com");
        await _service.CreateAsync("user2@test.com");
        await _service.CreateAsync("user3@test.com");

        // When: Listing with count of 2
        var result = await _service.ListAsync(count: 2);

        // Then: Only two invitations should be returned
        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    [Explicit("ListAsync is not yet implemented in EfInvitationService.")]
    public async Task ListAsync_WithOffsetAndCount_ReturnsPaginatedSlice()
    {
        // Given: Five invitations exist in the database
        await _service.CreateAsync("user1@test.com");
        await _service.CreateAsync("user2@test.com");
        await _service.CreateAsync("user3@test.com");
        await _service.CreateAsync("user4@test.com");
        await _service.CreateAsync("user5@test.com");

        // When: Listing page 2 with page size 2 (offset=2, count=2)
        var result = await _service.ListAsync(offset: 2, count: 2);

        // Then: Exactly two invitations should be returned
        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    [Explicit("ListAsync is not yet implemented in EfInvitationService.")]
    public async Task ListAsync_WithSearchTerm_FiltersByEmail()
    {
        // Given: Invitations with different email addresses
        await _service.CreateAsync("alice@example.com");
        await _service.CreateAsync("bob@example.com");
        await _service.CreateAsync("charlie@other.org");

        // When: Searching for "example"
        var result = await _service.ListAsync(searchTerm: "example");

        // Then: Only the two example.com invitations should be returned
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.All(e => e.Email!.Contains("example")), Is.True);
    }

    [Test]
    [Explicit("ListAsync is not yet implemented in EfInvitationService.")]
    public async Task ListAsync_WithSearchTerm_NoMatch_ReturnsEmpty()
    {
        // Given: Invitations that do not match the search term
        await _service.CreateAsync("alice@example.com");
        await _service.CreateAsync("bob@example.com");

        // When: Searching for a term that matches nothing
        var result = await _service.ListAsync(searchTerm: "zzznomatch");

        // Then: An empty list should be returned
        Assert.That(result, Is.Empty);
    }

    [Test]
    [Explicit("ListAsync is not yet implemented in EfInvitationService.")]
    public async Task ListAsync_WithStatusFilter_ReturnsPendingOnly()
    {
        // Given: A mix of pending and accepted invitations
        var pending = await _service.CreateAsync("pending@test.com");
        var accepted = await _service.CreateAsync("accepted@test.com");
        await _service.AcceptAsync(accepted, "user-123");

        // When: Filtering by Pending status
        var result = await _service.ListAsync(statusFilter: InvitationStatus.Pending);

        // Then: Only the pending invitation should be returned
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(pending.Id));
    }

    [Test]
    [Explicit("ListAsync is not yet implemented in EfInvitationService.")]
    public async Task ListAsync_WithStatusFilter_ReturnsAcceptedOnly()
    {
        // Given: A mix of pending and accepted invitations
        await _service.CreateAsync("pending@test.com");
        var accepted = await _service.CreateAsync("accepted@test.com");
        await _service.AcceptAsync(accepted, "user-123");

        // When: Filtering by Accepted status
        var result = await _service.ListAsync(statusFilter: InvitationStatus.Accepted);

        // Then: Only the accepted invitation should be returned
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(accepted.Id));
    }

    [Test]
    [Explicit("ListAsync is not yet implemented in EfInvitationService.")]
    public async Task ListAsync_WithStatusFilter_NoMatch_ReturnsEmpty()
    {
        // Given: Only pending invitations exist
        await _service.CreateAsync("user@test.com");

        // When: Filtering by Revoked status
        var result = await _service.ListAsync(statusFilter: InvitationStatus.Revoked);

        // Then: An empty list should be returned
        Assert.That(result, Is.Empty);
    }

    [Test]
    [Explicit("ListAsync is not yet implemented in EfInvitationService.")]
    public async Task ListAsync_WithSearchTermAndStatusFilter_AppliesBothFilters()
    {
        // Given: Multiple invitations with varying emails and statuses
        var pendingExample = await _service.CreateAsync("alice@example.com");
        var acceptedExample = await _service.CreateAsync("bob@example.com");
        await _service.AcceptAsync(acceptedExample, "user-123");
        await _service.CreateAsync("charlie@other.org");

        // When: Filtering by "example" email and Pending status
        var result = await _service.ListAsync(searchTerm: "example", statusFilter: InvitationStatus.Pending);

        // Then: Only the pending example.com invitation should be returned
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(pendingExample.Id));
    }

    [Test]
    [Explicit("ListAsync is not yet implemented in EfInvitationService.")]
    public async Task ListAsync_OffsetBeyondTotal_ReturnsEmpty()
    {
        // Given: Two invitations exist
        await _service.CreateAsync("user1@test.com");
        await _service.CreateAsync("user2@test.com");

        // When: Requesting with an offset beyond the total count
        var result = await _service.ListAsync(offset: 10);

        // Then: An empty list should be returned
        Assert.That(result, Is.Empty);
    }

    [Test]
    [Explicit("ListAsync is not yet implemented in EfInvitationService.")]
    public async Task ListAsync_ReturnsReadOnlyList()
    {
        // Given: One invitation exists
        await _service.CreateAsync("user@test.com");

        // When: Listing invitations
        var result = await _service.ListAsync();

        // Then: The result should implement IReadOnlyList<InvitationEntity>
        Assert.That(result, Is.AssignableTo<IReadOnlyList<InvitationEntity>>());
    }

    #endregion
}

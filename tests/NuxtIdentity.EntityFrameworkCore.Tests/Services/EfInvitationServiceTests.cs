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
    public async Task CreateAsync_WithExplicitStatus_UsesProvidedStatus()
    {
        // Given: An explicit status of Expired (for testing)

        // When: Creating an invitation with explicit status
        var entity = await _service.CreateAsync("user@test.com", new List<string>(),
            new List<ClaimInfo>(), TimeSpan.FromDays(7), status: InvitationStatus.Expired);

        // Then: The status should be Expired
        entity.Status.Should().Be(InvitationStatus.Expired);
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
        // Given: A revoked invitation (created with Revoked status for testing)
        var created = await _service.CreateAsync("user@test.com", new List<string>(),
            new List<ClaimInfo>(), TimeSpan.FromDays(7), status: InvitationStatus.Revoked);

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
        // Given: A revoked invitation
        var created = await _service.CreateAsync("user@test.com", new List<string>(),
            new List<ClaimInfo>(), TimeSpan.FromDays(7), status: InvitationStatus.Revoked);

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
}

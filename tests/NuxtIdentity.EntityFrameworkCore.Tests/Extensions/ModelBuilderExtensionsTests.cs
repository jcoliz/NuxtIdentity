using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Core.Models;
using NuxtIdentity.EntityFrameworkCore.Extensions;
using NuxtIdentity.EntityFrameworkCore.Tests.Helpers;

namespace NuxtIdentity.EntityFrameworkCore.Tests.Extensions;

[TestFixture]
[Category("Integration")]
public class ModelBuilderExtensionsTests
{
    private TestDbContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        // Given an in-memory database for testing
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new TestDbContext(options);
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
    }

    [Test]
    public void ConfigureNuxtIdentityRefreshTokens_ConfiguresEntity_EntityCanBeQueried()
    {
        // Given a database context with NuxtIdentity configuration applied
        // (This is done in TestDbContext.OnModelCreating)

        // When querying the RefreshTokens DbSet
        var query = _context.RefreshTokens.AsQueryable();

        // Then the query should be executable without errors
        Action act = () => query.ToList();
        act.Should().NotThrow();
    }

    [Test]
    public async Task ConfigureNuxtIdentityRefreshTokens_EntityConfiguration_SupportsBasicCrudOperations()
    {
        // Given a refresh token entity
        var entity = new RefreshTokenEntity
        {
            TokenHash = "test-hash",
            UserId = "user123",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        // When adding the entity to the database
        _context.RefreshTokens.Add(entity);
        await _context.SaveChangesAsync();

        // Then the entity should be retrievable
        var retrieved = await _context.RefreshTokens.FirstOrDefaultAsync();
        retrieved.Should().NotBeNull();
        retrieved!.TokenHash.Should().Be("test-hash");
        retrieved.UserId.Should().Be("user123");
        retrieved.IsRevoked.Should().BeFalse();
    }

    [Test]
    public async Task ConfigureNuxtIdentityRefreshTokens_TokenHashIndex_AllowsFastLookup()
    {
        // Given multiple tokens in the database
        var tokens = new[]
        {
            new RefreshTokenEntity
            {
                TokenHash = "hash1",
                UserId = "user1",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            },
            new RefreshTokenEntity
            {
                TokenHash = "hash2",
                UserId = "user2",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            },
            new RefreshTokenEntity
            {
                TokenHash = "hash3",
                UserId = "user3",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            }
        };

        _context.RefreshTokens.AddRange(tokens);
        await _context.SaveChangesAsync();

        // When querying by TokenHash (which should use the index)
        var result = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == "hash2");

        // Then the correct token should be found
        result.Should().NotBeNull();
        result!.UserId.Should().Be("user2");
    }

    [Test]
    public async Task ConfigureNuxtIdentityRefreshTokens_UserIdIndex_AllowsFastUserLookup()
    {
        // Given multiple tokens for different users
        var tokens = new[]
        {
            new RefreshTokenEntity
            {
                TokenHash = "hash1",
                UserId = "user1",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            },
            new RefreshTokenEntity
            {
                TokenHash = "hash2",
                UserId = "user1",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            },
            new RefreshTokenEntity
            {
                TokenHash = "hash3",
                UserId = "user2",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            }
        };

        _context.RefreshTokens.AddRange(tokens);
        await _context.SaveChangesAsync();

        // When querying by UserId (which should use the index)
        var results = await _context.RefreshTokens
            .Where(t => t.UserId == "user1")
            .ToListAsync();

        // Then all tokens for that user should be found
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(t => t.UserId.Should().Be("user1"));
    }

    [Test]
    public async Task ConfigureNuxtIdentityRefreshTokens_RequiredProperties_TokenHashIsRequired()
    {
        // Given a token entity without a TokenHash
        var entity = new RefreshTokenEntity
        {
            TokenHash = null!, // Intentionally null
            UserId = "user123",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        _context.RefreshTokens.Add(entity);

        // When attempting to save
        Func<Task> act = async () => await _context.SaveChangesAsync();

        // Then it should throw because TokenHash is required
        // EF Core 10 InMemory provider enforces nullability constraints
        await act.Should().ThrowAsync<DbUpdateException>()
            .WithMessage("*TokenHash*");
    }

    [Test]
    public async Task ConfigureNuxtIdentityRefreshTokens_RequiredProperties_UserIdIsRequired()
    {
        // Given a token entity without a UserId
        var entity = new RefreshTokenEntity
        {
            TokenHash = "test-hash",
            UserId = null!, // Intentionally null
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        _context.RefreshTokens.Add(entity);

        // When attempting to save
        Func<Task> act = async () => await _context.SaveChangesAsync();

        // Then it should throw because UserId is required
        // EF Core 10 InMemory provider enforces nullability constraints
        await act.Should().ThrowAsync<DbUpdateException>()
            .WithMessage("*UserId*");
    }

    [Test]
    public async Task ConfigureNuxtIdentityRefreshTokens_PrimaryKey_IdIsAutoGenerated()
    {
        // Given a token entity without an explicit Id
        var entity = new RefreshTokenEntity
        {
            TokenHash = "test-hash",
            UserId = "user123",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        // When adding and saving the entity
        _context.RefreshTokens.Add(entity);
        await _context.SaveChangesAsync();

        // Then the Id should be auto-generated
        entity.Id.Should().NotBe(0);

        // And it should be retrievable by Id
        var retrieved = await _context.RefreshTokens.FindAsync(entity.Id);
        retrieved.Should().NotBeNull();
        retrieved!.TokenHash.Should().Be("test-hash");
    }

    [Test]
    public async Task ConfigureNuxtIdentityRefreshTokens_MultipleEntities_UniqueIdsGenerated()
    {
        // Given multiple token entities
        var entities = new[]
        {
            new RefreshTokenEntity
            {
                TokenHash = "hash1",
                UserId = "user1",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            },
            new RefreshTokenEntity
            {
                TokenHash = "hash2",
                UserId = "user2",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            },
            new RefreshTokenEntity
            {
                TokenHash = "hash3",
                UserId = "user3",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            }
        };

        // When adding and saving the entities
        _context.RefreshTokens.AddRange(entities);
        await _context.SaveChangesAsync();

        // Then each should have a unique Id
        var ids = entities.Select(e => e.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().AllSatisfy(id => id.Should().NotBe(0));
    }

    [Test]
    public async Task ConfigureNuxtIdentityRefreshTokens_UpdateOperation_WorksCorrectly()
    {
        // Given a token entity in the database
        var entity = new RefreshTokenEntity
        {
            TokenHash = "test-hash",
            UserId = "user123",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        _context.RefreshTokens.Add(entity);
        await _context.SaveChangesAsync();

        // When updating the entity
        entity.IsRevoked = true;
        entity.ExpiresAt = DateTime.UtcNow.AddDays(1);
        await _context.SaveChangesAsync();

        // Then the changes should be persisted
        var retrieved = await _context.RefreshTokens.FindAsync(entity.Id);
        retrieved.Should().NotBeNull();
        retrieved!.IsRevoked.Should().BeTrue();
        retrieved.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(1), TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task ConfigureNuxtIdentityRefreshTokens_DeleteOperation_WorksCorrectly()
    {
        // Given a token entity in the database
        var entity = new RefreshTokenEntity
        {
            TokenHash = "test-hash",
            UserId = "user123",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        _context.RefreshTokens.Add(entity);
        await _context.SaveChangesAsync();
        var entityId = entity.Id;

        // When deleting the entity
        _context.RefreshTokens.Remove(entity);
        await _context.SaveChangesAsync();

        // Then the entity should no longer exist
        var retrieved = await _context.RefreshTokens.FindAsync(entityId);
        retrieved.Should().BeNull();
    }

    #region Invitation Entity Tests

    [Test]
    public void ConfigureNuxtIdentityInvitations_ConfiguresEntity_EntityCanBeQueried()
    {
        // Given: A database context with NuxtIdentity invitation configuration applied

        // When: Querying the Invitations DbSet
        var query = _context.Invitations.AsQueryable();

        // Then: The query should be executable without errors
        Action act = () => query.ToList();
        act.Should().NotThrow();
    }

    [Test]
    public async Task ConfigureNuxtIdentityInvitations_EntityConfiguration_SupportsBasicCrudOperations()
    {
        // Given: An invitation entity
        var entity = new InvitationEntity
        {
            Code = Guid.NewGuid(),
            Email = "user@test.com",
            Status = InvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        // When: Adding the entity to the database
        _context.Invitations.Add(entity);
        await _context.SaveChangesAsync();

        // Then: The entity should be retrievable
        var retrieved = await _context.Invitations.FirstOrDefaultAsync();
        retrieved.Should().NotBeNull();
        retrieved!.Email.Should().Be("user@test.com");
        retrieved.Status.Should().Be(InvitationStatus.Pending);
    }

    [Test]
    public async Task ConfigureNuxtIdentityInvitations_RoundTrip_StoresAndRetrievesAllProperties()
    {
        // Given: An invitation entity with all properties set
        var entity = new InvitationEntity
        {
            Code = Guid.NewGuid(),
            Email = "user@test.com",
            Status = InvitationStatus.Pending,
            Roles = """["Admin","User"]""",
            Claims = """[{"Type":"dept","Value":"eng"}]""",
            Metadata = """{"source":"test"}""",
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc),
            AcceptedAt = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            AcceptedByUserId = "user-123"
        };

        // When: Storing and retrieving the entity
        _context.Invitations.Add(entity);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var retrieved = await _context.Invitations.FirstAsync();

        // Then: All properties should round-trip correctly
        retrieved.Code.Should().Be(entity.Code);
        retrieved.Email.Should().Be("user@test.com");
        retrieved.Status.Should().Be(InvitationStatus.Pending);
        retrieved.Roles.Should().Be("""["Admin","User"]""");
        retrieved.Claims.Should().Be("""[{"Type":"dept","Value":"eng"}]""");
        retrieved.Metadata.Should().Be("""{"source":"test"}""");
        retrieved.CreatedAt.Should().Be(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        retrieved.ExpiresAt.Should().Be(new DateTime(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc));
        retrieved.AcceptedAt.Should().Be(new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        retrieved.AcceptedByUserId.Should().Be("user-123");
    }

    #endregion
}

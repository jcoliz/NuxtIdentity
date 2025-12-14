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
}

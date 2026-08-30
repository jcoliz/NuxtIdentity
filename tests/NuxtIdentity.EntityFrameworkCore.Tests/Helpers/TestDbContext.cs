using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Core.Models;
using NuxtIdentity.EntityFrameworkCore.Extensions;

namespace NuxtIdentity.EntityFrameworkCore.Tests.Helpers;

/// <summary>
/// Test database context for integration tests.
/// </summary>
public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    public DbSet<TestUserEntity> Users => Set<TestUserEntity>();

    public DbSet<InvitationEntity> Invitations => Set<InvitationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureNuxtIdentityRefreshTokens();
        modelBuilder.ConfigureNuxtIdentityInvitations();
    }
}

/// <summary>
/// Minimal user shape used in EF integration tests for joins by user ID.
/// </summary>
public class TestUserEntity
{
    public string Id { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;
}

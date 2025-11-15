using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Core.Models;
using NuxtIdentity.Playground.Local.Models;

namespace NuxtIdentity.Playground.Local.Data;

/// <summary>
/// Database context for the application including Identity tables.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the refresh tokens.
    /// </summary>
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash);
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.TokenHash).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
        });
    }
}
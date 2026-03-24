using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for configuring NuxtIdentity entities in Entity Framework Core.
/// </summary>
public static class NuxtIdentityModelBuilderExtensions
{
    /// <summary>
    /// Configures the RefreshTokenEntity for use with Entity Framework Core.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The model builder for chaining.</returns>
    /// <remarks>
    /// This method configures:
    /// - Primary key on Id
    /// - Index on TokenHash for fast lookups
    /// - Index on UserId for fast user-based queries
    /// - Required properties (TokenHash, UserId)
    /// 
    /// Call this in your DbContext's OnModelCreating method:
    /// <code>
    /// protected override void OnModelCreating(ModelBuilder builder)
    /// {
    ///     base.OnModelCreating(builder);
    ///     builder.ConfigureNuxtIdentityRefreshTokens();
    /// }
    /// </code>
    /// </remarks>
    public static ModelBuilder ConfigureNuxtIdentityRefreshTokens(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasIndex(e => e.TokenHash);
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.Key).IsRequired();
            entity.Property(e => e.TokenHash).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.IsRevoked).IsRequired();
        });

        return modelBuilder;
    }

    /// <summary>
    /// Configures the InvitationEntity for use with Entity Framework Core.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The model builder for chaining.</returns>
    /// <remarks>
    /// This method configures:
    /// - Primary key on Id
    /// - Unique index on Code for fast lookups
    /// - Indexes on Email and Status for admin queries
    /// - Required properties (Code, Email, Status, CreatedAt, ExpiresAt)
    /// - Max length constraints on Email (256), Roles/Claims/Metadata (4000)
    ///
    /// Call this in your DbContext's OnModelCreating method:
    /// <code>
    /// protected override void OnModelCreating(ModelBuilder builder)
    /// {
    ///     base.OnModelCreating(builder);
    ///     builder.ConfigureNuxtIdentityInvitations();
    /// }
    /// </code>
    /// </remarks>
    public static ModelBuilder ConfigureNuxtIdentityInvitations(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InvitationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.Code).IsRequired();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.Roles).HasMaxLength(4000);
            entity.Property(e => e.Claims).HasMaxLength(4000);
            entity.Property(e => e.Metadata).HasMaxLength(4000);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
        });

        return modelBuilder;
    }
}
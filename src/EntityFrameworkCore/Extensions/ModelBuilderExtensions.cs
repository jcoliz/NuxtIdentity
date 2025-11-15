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
            entity.HasIndex(e => e.TokenHash);
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.TokenHash).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.IsRevoked).IsRequired();
        });

        return modelBuilder;
    }
}
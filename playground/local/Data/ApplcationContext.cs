using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;
using NuxtIdentity.EntityFrameworkCore.Extensions;

namespace NuxtIdentity.Playground.Local.Data;

/// <summary>
/// Database context for the application including Identity tables.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<IdentityUser>, IDbContextCleaner
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

        // Use the extension method from the library
        builder.ConfigureNuxtIdentityRefreshTokens();
    }

    /// <summary>
    /// Clears the Entity Framework DbContext change tracker, stopping tracking of all entities.
    /// </summary>
    public void ClearChangeTracker()
    {
        ChangeTracker.Clear();
    }
}

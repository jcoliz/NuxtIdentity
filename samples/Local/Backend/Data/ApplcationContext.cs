using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Core.Models;
using NuxtIdentity.EntityFrameworkCore.Extensions;

namespace NuxtIdentity.Samples.Local.Data;

/// <summary>
/// Database context for the application including Identity tables.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<IdentityUser>
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
}
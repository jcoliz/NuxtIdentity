using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.EntityFrameworkCore.Extensions;

namespace NuxtIdentity.AspNetCore.Tests.Helpers;

/// <summary>
/// Test database context for integration tests.
/// </summary>
public class TestDbContext : IdentityDbContext<TestUser>
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Add NuxtIdentity refresh token configuration
        builder.ConfigureNuxtIdentityRefreshTokens();
    }
}

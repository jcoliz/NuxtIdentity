using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Tenancy.Models;

namespace NuxtIdentity.Tenancy.Extensions;

public static class TenancyModelBuilderExtensions
{
    public static ModelBuilder ConfigureNuxtIdentityTenancy(this ModelBuilder modelBuilder)
    {
        // Configure tenancy entities
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });
        
        modelBuilder.Entity<UserTenantRole>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.TenantId }).IsUnique();
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
        });
        
        return modelBuilder;
    }
}
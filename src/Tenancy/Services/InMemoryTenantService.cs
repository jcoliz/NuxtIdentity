using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using NuxtIdentity.Tenancy.Abstractions;
using NuxtIdentity.Tenancy.Models;

namespace NuxtIdentity.Tenancy.Services;

/// <summary>
/// In-memory implementation of tenant service using collections.
/// </summary>
/// <remarks>
/// This implementation is suitable for development, testing, or small applications.
/// Data is lost when the application restarts.
/// </remarks>
public partial class InMemoryTenantService<TUser> : ITenantService<TUser> where TUser : IdentityUser
{
    private readonly List<Tenant> _tenants = [];
    private readonly List<UserTenantRole> _userTenantRoles = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    public Task<UserTenantRole> AddUserToTenantAsync(Guid tenantId, string userId, TenantRole role, string invitedByUserId)
    {
        throw new NotImplementedException();
    }

    public Task<Tenant> CreateTenantAsync(string name, string creatorUserId)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<Claim>> GetClaimsAsync(TUser user)
    {
        var userTenants = await GetUserTenantsAsync(user.Id);
        return userTenants.Select(ut => new Claim("entitlement", $"tenant:{ut.TenantId}:{ut.Role.ToString().ToLower()}"));
    }

    public Task<Tenant?> GetTenantAsync(Guid tenantId, string userId)
    {
        throw new NotImplementedException();
    }

    public Task<TenantRole?> GetUserTenantRoleAsync(Guid tenantId, string userId)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<UserTenantRole>> GetUserTenantsAsync(string userId)
    {
        await _lock.WaitAsync();
        try
        {
            return _userTenantRoles
                .Where(utr => utr.UserId == userId && _tenants.Any(t => t.Id == utr.TenantId && t.IsActive))
                .Select(utr => new UserTenantRole
                {
                    Id = utr.Id,
                    UserId = utr.UserId,
                    TenantId = utr.TenantId,
                    Role = utr.Role,
                    CreatedAt = utr.CreatedAt,
                    Tenant = _tenants.First(t => t.Id == utr.TenantId)
                })
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<bool> HasTenantAccessAsync(Guid tenantId, string userId, TenantRole minimumRole)
    {
        throw new NotImplementedException();
    }
}

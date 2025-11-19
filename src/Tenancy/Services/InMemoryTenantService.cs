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
public partial class InMemoryTenantService : ITenantService
{
    private readonly List<Tenant> _tenants = [];
    private readonly List<UserTenantRole> _userTenantRoles = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

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

    // ... implement other methods following same pattern
}
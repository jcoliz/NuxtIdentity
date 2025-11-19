using NuxtIdentity.Tenancy.Models;

namespace NuxtIdentity.Tenancy.Abstractions;

/// <summary>
/// Service for managing tenant operations and user-tenant relationships.
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Gets all tenants that a user has access to with their roles.
    /// </summary>
    /// <param name="userId">The user ID to get tenants for.</param>
    /// <returns>Collection of user tenant relationships.</returns>
    Task<IEnumerable<UserTenantRole>> GetUserTenantsAsync(string userId);
    
    /// <summary>
    /// Gets a specific tenant by ID if the user has access.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="userId">The user ID requesting access.</param>
    /// <returns>The tenant if user has access; otherwise null.</returns>
    Task<Tenant?> GetTenantAsync(Guid tenantId, string userId);
    
    /// <summary>
    /// Creates a new tenant with the creator as Owner.
    /// </summary>
    /// <param name="name">The tenant name.</param>
    /// <param name="creatorUserId">The user ID of the creator.</param>
    /// <returns>The created tenant.</returns>
    Task<Tenant> CreateTenantAsync(string name, string creatorUserId);
    
    /// <summary>
    /// Adds a user to a tenant with the specified role.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="userId">The user ID to add.</param>
    /// <param name="role">The role to assign.</param>
    /// <param name="invitedByUserId">The user ID of who is adding them (must be Owner).</param>
    /// <returns>The created user-tenant relationship.</returns>
    Task<UserTenantRole> AddUserToTenantAsync(Guid tenantId, string userId, TenantRole role, string invitedByUserId);
    
    /// <summary>
    /// Gets the user's role for a specific tenant.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The user's role, or null if no access.</returns>
    Task<TenantRole?> GetUserTenantRoleAsync(Guid tenantId, string userId);
    
    /// <summary>
    /// Checks if a user has the minimum required role for a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="minimumRole">The minimum role required.</param>
    /// <returns>True if user has sufficient access.</returns>
    Task<bool> HasTenantAccessAsync(Guid tenantId, string userId, TenantRole minimumRole);
}
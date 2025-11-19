namespace NuxtIdentity.Tenancy.Services;

public class TenantClaimsProvider<TUser> : IUserClaimsProvider<TUser> where TUser : IdentityUser
{
    public async Task<IEnumerable<Claim>> GetClaimsAsync(TUser user)
    {
        var userTenants = await _tenantService.GetUserTenantsAsync(user.Id);
        
        // Add entitlements claim as described in your ADR
        var entitlements = userTenants.Select(ut => $"{ut.TenantId}:{ut.Role.ToString().ToLower()}");
        return [ new Claim("tenants", string.Join(",", entitlements)) ];                
    }
}
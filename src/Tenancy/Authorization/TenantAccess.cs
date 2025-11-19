using Microsoft.AspNetCore.Authorization;

namespace NuxtIdentity.Tenancy.Authorization;

public class TenantAccessRequirement : IAuthorizationRequirement
{
    public TenantRole MinimumRole { get; }
    public TenantAccessRequirement(TenantRole minimumRole) => MinimumRole = minimumRole;
}

public class TenantAccessHandler : AuthorizationHandler<TenantAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantAccessRequirement requirement)
    {
        var tenantId = GetTenantIdFromContext(context);
        var userRole = GetUserTenantRole(context.User, tenantId);
        
        if (userRole >= requirement.MinimumRole)
        {
            context.Succeed(requirement);
        }
        
        return Task.CompletedTask;
    }
}

// Extension methods for easy policy setup
public static class TenancyPolicyExtensions
{
    public static void AddTenancyPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy("TenantView", policy => 
            policy.Requirements.Add(new TenantAccessRequirement(TenantRole.Viewer)));
        options.AddPolicy("TenantEdit", policy => 
            policy.Requirements.Add(new TenantAccessRequirement(TenantRole.Editor)));
        options.AddPolicy("TenantOwn", policy => 
            policy.Requirements.Add(new TenantAccessRequirement(TenantRole.Owner)));
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using NuxtIdentity.Tenancy.Abstractions;
using NuxtIdentity.Tenancy.Models;
using NuxtIdentity.Tenancy.Services;

namespace NuxtIdentity.Tenancy.Authorization;

/// <summary>
/// Authorization requirement that checks if a user has a minimum role within a tenant.
/// </summary>
public class TenantAccessRequirement : IAuthorizationRequirement
{
    public TenantRole MinimumRole { get; }
    public TenantAccessRequirement(TenantRole minimumRole) => MinimumRole = minimumRole;
}

/// <summary>
/// Handles authorization for tenant access requirements by verifying the user's role within the tenant context.
/// Register this handler in your DI container: services.AddSingleton&lt;IAuthorizationHandler, TenantAccessHandler&lt;TUser&gt;&gt;();
/// </summary>
public class TenantAccessHandler<IdentityUser> : AuthorizationHandler<TenantAccessRequirement>
{
    private readonly ITenantService<IdentityUser> _tenantService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantAccessHandler(ITenantService<IdentityUser> tenantService, IHttpContextAccessor httpContextAccessor)
    {
        _tenantService = tenantService;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantAccessRequirement requirement)
    {
        var tenantId = GetTenantIdFromContext(context);
        if (tenantId == null)
        {
            return;
        }

        var userRole = await _tenantService.GetUserTenantRoleAsync(tenantId.Value, context.User);
        
        if (userRole >= requirement.MinimumRole)
        {
            context.Succeed(requirement);
        }
    }

    private Guid? GetTenantIdFromContext(AuthorizationHandlerContext context)
    {
        // Try to get from route data first (e.g., /tenants/{tenantId}/...)
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.GetRouteData()?.Values.TryGetValue("tenantId", out var routeTenantId) == true)
        {
            if (Guid.TryParse(routeTenantId?.ToString(), out var tenantId))
            {
                return tenantId;
            }
        }

        // Try to get from resource if it implements ITenantResource
        if (context.Resource is ITenantResource tenantResource)
        {
            return tenantResource.TenantId;
        }

        return null;
    }
}

/// <summary>
/// Extension methods for configuring tenant-based authorization policies.
/// </summary>
public static class TenancyPolicyExtensions
{
    /// <summary>
    /// Adds predefined tenant access policies: TenantView, TenantEdit, and TenantOwn.
    /// Usage: In Program.cs or Startup.cs, call builder.Services.AddAuthorization(options => options.AddTenancyPolicies());
    /// Then use [Authorize(Policy = "TenantView")] on controllers or actions.
    /// </summary>
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
namespace NuxtIdentity.Tenancy.Extensions;

public static class TenancyServiceCollectionExtensions
{
    public static IServiceCollection AddNuxtIdentityTenancy<TUser, TContext>(
        this IServiceCollection services)
        where TUser : IdentityUser, new()
        where TContext : DbContext
    {
        // Add the base NuxtIdentity services first
        services.AddNuxtIdentity<TUser>();
        
        // Add tenancy-specific services
        services.AddScoped<ITenantService, TenantService<TContext>>();
        services.AddScoped<IUserClaimsProvider<TUser>, TenantClaimsProvider<TUser>>();
        
        // Add authorization handlers
        services.AddScoped<IAuthorizationHandler, TenantAccessHandler>();
        
        return services;
    }
}
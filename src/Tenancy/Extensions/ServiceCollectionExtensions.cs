using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NuxtIdentity.AspNetCore.Extensions;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Tenancy.Abstractions;
using NuxtIdentity.Tenancy.Authorization;
using NuxtIdentity.Tenancy.Services;

namespace NuxtIdentity.Tenancy.Extensions;

public static class TenancyServiceCollectionExtensions
{
    /// <summary>
    /// Adds NuxtIdentity Tenancy services to the service collection using an in-memory tenant service.
    /// </summary>
    /// <typeparam name="TUser"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddNuxtIdentityTenancy<TUser>(
        this IServiceCollection services)
        where TUser : IdentityUser, new()
    {
        // Add the base NuxtIdentity services first
        // TODO: Reconsider. I think we'd already have done this!!!
        services.AddNuxtIdentity<TUser>();
        
        // Add tenancy-specific services
        services.AddScoped<ITenantService<TUser>, InMemoryTenantService<TUser>>();
        services.AddScoped<IUserClaimsProvider<TUser>, InMemoryTenantService<TUser>>();
        
        // Add authorization handlers
        services.AddScoped<IAuthorizationHandler, TenantAccessHandler>();
        
        return services;
    }

#if false
    /// <summary>
    /// Adds NuxtIdentity Tenancy services to the service collection using a database-backed tenant service.
    /// </summary>
    /// <typeparam name="TUser"></typeparam>
    /// <typeparam name="TContext"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddNuxtIdentityTenancy<TUser, TContext>(
        this IServiceCollection services)
        where TUser : IdentityUser, new()
        where TContext : DbContext
    {
        // Add the base NuxtIdentity services first
        services.AddNuxtIdentity<TUser>();
        
        // Add tenancy-specific services
        services.AddScoped<ITenantService, TenantService<TContext>>();
        services.AddScoped<IUserClaimsProvider<TUser>, InMemoryTenantService<TUser>>();
        
        // Add authorization handlers
        services.AddScoped<IAuthorizationHandler, TenantAccessHandler>();
        
        return services;
    }
#endif
}
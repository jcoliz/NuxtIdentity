using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.EntityFrameworkCore.Services;

namespace NuxtIdentity.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering NuxtIdentity Entity Framework Core services.
/// </summary>
public static class NuxtIdentityServiceCollectionExtensions
{
    /// <summary>
    /// Adds NuxtIdentity Entity Framework Core services to the dependency injection container.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type that contains RefreshTokens DbSet.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registers:
    /// - <see cref="EfRefreshTokenService{TContext}"/> as <see cref="IRefreshTokenService"/>
    /// 
    /// Your DbContext must have a DbSet&lt;RefreshTokenEntity&gt; configured and should call
    /// modelBuilder.ConfigureNuxtIdentityRefreshTokens() in OnModelCreating.
    /// 
    /// Example usage:
    /// <code>
    /// services.AddDbContext&lt;ApplicationDbContext&gt;(options => ...);
    /// services.AddNuxtIdentityEntityFramework&lt;ApplicationDbContext&gt;();
    /// </code>
    /// </remarks>
    public static IServiceCollection AddNuxtIdentityEntityFramework<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddScoped<IRefreshTokenService, EfRefreshTokenService<TContext>>();
        
        return services;
    }
}
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuxtIdentity.AspNetCore.Extensions;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.EntityFrameworkCore.Services;

namespace NuxtIdentity.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering NuxtIdentity Entity Framework Core services.
/// </summary>
public static class NuxtIdentityServiceCollectionExtensions
{
    /// <summary>
    /// Adds all NuxtIdentity services with Entity Framework Core support in a single call.
    /// </summary>
    /// <typeparam name="TUser">The user type, must derive from IdentityUser.</typeparam>
    /// <typeparam name="TContext">The DbContext type that contains RefreshTokens DbSet.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration containing JWT options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This is a convenience method that combines:
    /// - NuxtIdentity core services (AddNuxtIdentity)
    /// - Entity Framework Core integration (AddNuxtIdentityEntityFramework)
    /// - JWT Bearer authentication with configuration (AddNuxtIdentityAuthentication)
    /// 
    /// Prerequisites:
    /// - ASP.NET Core Identity must be configured with AddIdentity&lt;TUser, TRole&gt;()
    /// - DbContext must have RefreshTokens DbSet and call modelBuilder.ConfigureNuxtIdentityRefreshTokens()
    /// - JWT options must be present in appsettings.json under "Jwt" section
    /// 
    /// Example usage:
    /// <code>
    /// builder.Services.AddIdentity&lt;IdentityUser, IdentityRole&gt;()
    ///     .AddEntityFrameworkStores&lt;ApplicationDbContext&gt;();
    /// 
    /// builder.Services.AddNuxtIdentityWithEntityFramework&lt;IdentityUser, ApplicationDbContext&gt;(
    ///     builder.Configuration);
    /// </code>
    /// 
    /// This replaces the following separate calls:
    /// <code>
    /// builder.Services.Configure&lt;JwtOptions&gt;(
    ///     builder.Configuration.GetSection(JwtOptions.SectionName));
    /// builder.Services.AddNuxtIdentity&lt;IdentityUser&gt;();
    /// builder.Services.AddNuxtIdentityEntityFramework&lt;ApplicationDbContext&gt;();
    /// builder.Services.AddNuxtIdentityAuthentication();
    /// </code>
    /// </remarks>
    public static IServiceCollection AddNuxtIdentityWithEntityFramework<TUser, TContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TUser : IdentityUser
        where TContext : DbContext
    {
        // Add NuxtIdentity core services
        services.AddNuxtIdentity<TUser>();

        // Add Entity Framework Core integration
        services.AddNuxtIdentityEntityFramework<TContext>();

        // Add JWT Bearer authentication (includes JWT options configuration)
        services.AddNuxtIdentityAuthentication(configuration);

        return services;
    }

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
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NuxtIdentity.AspNetCore.Configuration;
using NuxtIdentity.AspNetCore.Services;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Services;

namespace NuxtIdentity.AspNetCore.Extensions;

/// <summary>
/// Extension methods for registering NuxtIdentity ASP.NET Core services.
/// </summary>
public static class NuxtIdentityServiceCollectionExtensions
{
    /// <summary>
    /// Adds NuxtIdentity JWT Bearer authentication to the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This configures JWT Bearer authentication as the default authentication scheme.
    /// JWT options are configured from the "Jwt" section in appsettings.json.
    /// 
    /// Example appsettings.json:
    /// <code>
    /// {
    ///   "Jwt": {
    ///     "SecretKey": "your-secret-key-min-32-chars",
    ///     "Issuer": "your-app",
    ///     "Audience": "your-app-users",
    ///     "ExpirationMinutes": 60
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddNuxtIdentityAuthentication(
        this IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer();
        
        services.ConfigureOptions<JwtBearerOptionsSetup>();
        
        return services;
    }

    /// <summary>
    /// Adds NuxtIdentity services for ASP.NET Core Identity integration.
    /// </summary>
    /// <typeparam name="TUser">The user type, must derive from IdentityUser.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registers:
    /// - <see cref="IJwtTokenService{TUser}"/> - JWT token generation and validation
    /// - <see cref="IUserClaimsProvider{TUser}"/> - Claims extraction from Identity users
    /// 
    /// Prerequisites:
    /// - ASP.NET Core Identity must be configured with AddIdentity&lt;TUser, TRole&gt;()
    /// - UserManager&lt;TUser&gt; must be available in DI
    /// - JWT options must be configured in appsettings.json
    /// 
    /// Example usage:
    /// <code>
    /// builder.Services.AddIdentity&lt;ApplicationUser, IdentityRole&gt;()
    ///     .AddEntityFrameworkStores&lt;ApplicationDbContext&gt;();
    /// 
    /// builder.Services.Configure&lt;JwtOptions&gt;(
    ///     builder.Configuration.GetSection(JwtOptions.SectionName));
    /// 
    /// builder.Services.AddNuxtIdentity&lt;ApplicationUser&gt;();
    /// builder.Services.AddNuxtIdentityAuthentication();
    /// </code>
    /// </remarks>
    public static IServiceCollection AddNuxtIdentity<TUser>(
        this IServiceCollection services)
        where TUser : IdentityUser
    {
        services.AddScoped<IUserClaimsProvider<TUser>, IdentityUserClaimsProvider<TUser>>();
        services.AddScoped<IJwtTokenService<TUser>, JwtTokenService<TUser>>();
        
        return services;
    }
}
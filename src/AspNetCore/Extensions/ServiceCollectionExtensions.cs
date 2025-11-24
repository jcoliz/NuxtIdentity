using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuxtIdentity.AspNetCore.Configuration;
using NuxtIdentity.AspNetCore.Services;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Configuration;
using NuxtIdentity.Core.Services;

namespace NuxtIdentity.AspNetCore.Extensions;

/// <summary>
/// Extension methods for registering NuxtIdentity ASP.NET Core services.
/// </summary>
public static partial class NuxtIdentityServiceCollectionExtensions
{
    /// <summary>
    /// Adds NuxtIdentity JWT Bearer authentication to the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration containing JWT options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This configures JWT Bearer authentication as the default authentication scheme
    /// and automatically configures JWT options from the "Jwt" section in appsettings.json.
    /// 
    /// Features included:
    /// - JWT options configuration from appsettings.json
    /// - JWT Bearer authentication configuration
    /// - Enhanced logging for authentication failures and successes
    /// - Detailed error logging in development environments
    /// 
    /// Example appsettings.json:
    /// <code>
    /// {
    ///   "Jwt": {
    ///     "Key": "your-secret-key-min-32-chars",
    ///     "Issuer": "your-app",
    ///     "Audience": "your-app-users",
    ///     "ExpirationHours": 1
    ///   }
    /// }
    /// </code>
    /// 
    /// Example usage:
    /// <code>
    /// builder.Services.AddNuxtIdentityAuthentication(builder.Configuration);
    /// </code>
    /// </remarks>
    public static IServiceCollection AddNuxtIdentityAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure JWT options from configuration
        services.Configure<JwtOptions>(
            configuration.GetSection(JwtOptions.SectionName));

        // Add authentication with the parameterless overload
        return services.AddNuxtIdentityAuthentication();
    }

    /// <summary>
    /// Adds NuxtIdentity JWT Bearer authentication to the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This configures JWT Bearer authentication as the default authentication scheme.
    /// 
    /// Note: When using this overload, you must manually configure JWT options before calling this method:
    /// <code>
    /// builder.Services.Configure&lt;JwtOptions&gt;(
    ///     builder.Configuration.GetSection(JwtOptions.SectionName));
    /// builder.Services.AddNuxtIdentityAuthentication();
    /// </code>
    /// 
    /// For convenience, consider using the overload that takes IConfiguration:
    /// <code>
    /// builder.Services.AddNuxtIdentityAuthentication(builder.Configuration);
    /// </code>
    /// 
    /// Features included:
    /// - JWT Bearer authentication configuration
    /// - Enhanced logging for authentication failures and successes
    /// - Detailed error logging in development environments
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
        
        // Add enhanced JWT Bearer events for logging
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerEvents>>();
                    
                    LogJwtAuthenticationFailed(logger, context.Exception, 
                        context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                    
                    return Task.CompletedTask;
                },
                
                OnChallenge = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerEvents>>();
                        
                    LogJwtChallenge(logger, context.Error ?? "unknown", 
                        context.ErrorDescription ?? "unknown", context.Request.Path);
                        
                    return Task.CompletedTask;
                },
                
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerEvents>>();
                        
                    var username = context.Principal?.Identity?.Name ?? "unknown";
                    LogJwtTokenValidated(logger, username);
                    
                    return Task.CompletedTask;
                }
            };
        });
        
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
    /// - JWT options must be configured (use AddNuxtIdentityAuthentication with IConfiguration)
    /// 
    /// Example usage:
    /// <code>
    /// builder.Services.AddIdentity&lt;ApplicationUser, IdentityRole&gt;()
    ///     .AddEntityFrameworkStores&lt;ApplicationDbContext&gt;();
    /// 
    /// builder.Services.AddNuxtIdentity&lt;ApplicationUser&gt;();
    /// builder.Services.AddNuxtIdentityAuthentication(builder.Configuration);
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

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Warning, Message = "JWT Authentication failed for token from {remoteIp}")]
    private static partial void LogJwtAuthenticationFailed(ILogger logger, Exception exception, string remoteIp);

    [LoggerMessage(Level = LogLevel.Information, Message = "JWT Challenge triggered: {error} {errorDescription} for path {path}")]
    private static partial void LogJwtChallenge(ILogger logger, string error, string errorDescription, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "JWT token validated successfully for user: {username}")]
    private static partial void LogJwtTokenValidated(ILogger logger, string username);

    #endregion
}
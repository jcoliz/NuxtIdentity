using System.IdentityModel.Tokens.Jwt;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    /// <param name="environment">Optional hosting environment (used to determine if JWT configuration auto-generation is allowed). If null, defaults to treating environment as production.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This configures JWT Bearer authentication as the default authentication scheme
    /// and automatically configures JWT options from the "Jwt" section in appsettings.json.
    ///
    /// <para><strong>Auto-Generation in Non-Production Environments:</strong></para>
    /// In non-production environments (Development, Staging, Testing), JWT configuration will be
    /// auto-generated if not provided, eliminating boilerplate configuration:
    /// - <strong>Key:</strong> Auto-generates a secure 32-byte random key (tokens won't persist across restarts)
    /// - <strong>Issuer:</strong> Defaults to "NuxtIdentity.{EnvironmentName}" (e.g., "NuxtIdentity.Development")
    /// - <strong>Audience:</strong> Defaults to "NuxtIdentity.{EnvironmentName}" (e.g., "NuxtIdentity.Development")
    ///
    /// <para><strong>Production Requirements:</strong></para>
    /// In Production, all JWT configuration must be explicitly provided in appsettings.json.
    /// The application will fail to start with clear error messages if any required values are missing.
    ///
    /// Features included:
    /// - JWT options configuration from appsettings.json
    /// - Auto-generation of missing JWT options in non-production environments
    /// - JWT options validation at startup (validates Key, Issuer, Audience)
    /// - JWT Bearer authentication configuration
    /// - Enhanced logging for authentication failures and successes
    /// - Startup warning when JWT options are auto-generated
    ///
    /// Example appsettings.json (Production):
    /// <code>
    /// {
    ///   "Jwt": {
    ///     "Key": "your-base64-encoded-32-byte-key",
    ///     "Issuer": "your-app",
    ///     "Audience": "your-app-users",
    ///     "Lifespan": "01:00:00"
    ///   }
    /// }
    /// </code>
    ///
    /// Example appsettings.Development.json (Optional - will auto-generate if omitted):
    /// <code>
    /// {
    ///   "Jwt": {}
    /// }
    /// </code>
    ///
    /// Example usage:
    /// <code>
    /// builder.Services.AddNuxtIdentityAuthentication(builder.Configuration, builder.Environment);
    /// </code>
    /// </remarks>
    public static IServiceCollection AddNuxtIdentityAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        var isProduction = environment?.IsProduction() ?? false;
        
        // Configure JWT options from configuration with validation
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .PostConfigure(options =>
            {
                // Auto-generate JWT configuration in non-production environments if not configured
                if (!isProduction)
                {
                    var environmentName = environment?.EnvironmentName ?? "Development";
                    
                    if (options.Key == null || options.Key.Length == 0)
                    {
                        options.Key = RandomNumberGenerator.GetBytes(32);
                    }
                    
                    if (string.IsNullOrWhiteSpace(options.Issuer))
                    {
                        options.Issuer = $"NuxtIdentity.{environmentName}";
                    }
                    
                    if (string.IsNullOrWhiteSpace(options.Audience))
                    {
                        options.Audience = $"NuxtIdentity.{environmentName}";
                    }
                }
            })
            .Validate(options =>
            {
                // In production, key is always required
                if (isProduction)
                {
                    return options.Key != null && options.Key.Length > 0;
                }
                
                // In non-production, key will be auto-generated if missing, so it should always exist at this point
                return options.Key != null && options.Key.Length > 0;
            }, isProduction
                ? "JWT signing key is required in Production. Configure Jwt:Key in appsettings.json with a Base64-encoded 32-byte value. " +
                  "Generate one using: [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))"
                : "JWT signing key validation failed unexpectedly in non-production environment.")
            .Validate(options =>
            {
                return options.Key == null || options.Key.Length >= 32;
            }, "JWT signing key must be at least 32 bytes (256 bits) for HMAC-SHA256 security. " +
               "Generate a secure key using: [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))")
            .Validate(options =>
            {
                // In production, issuer is always required
                if (isProduction)
                {
                    return !string.IsNullOrWhiteSpace(options.Issuer);
                }
                
                // In non-production, issuer will be auto-generated if missing, so it should always exist
                return !string.IsNullOrWhiteSpace(options.Issuer);
            }, isProduction
                ? "JWT Issuer is required in Production. Configure Jwt:Issuer in appsettings.json with a unique value for your application."
                : "JWT Issuer validation failed unexpectedly in non-production environment.")
            .Validate(options =>
            {
                // In production, audience is always required
                if (isProduction)
                {
                    return !string.IsNullOrWhiteSpace(options.Audience);
                }
                
                // In non-production, audience will be auto-generated if missing, so it should always exist
                return !string.IsNullOrWhiteSpace(options.Audience);
            }, isProduction
                ? "JWT Audience is required in Production. Configure Jwt:Audience in appsettings.json with a unique value for your application."
                : "JWT Audience validation failed unexpectedly in non-production environment.")
            .Validate(options => options.ClockSkew >= TimeSpan.Zero && options.ClockSkew <= TimeSpan.FromMinutes(5),
                "JWT ClockSkew must be between 00:00:00 and 00:05:00. Use a small value (for example 00:00:30) to tolerate minor clock drift.")
            .ValidateOnStart();

        // Add logging to warn about auto-generated key (done via separate service)
        if (!isProduction)
        {
            services.AddHostedService<JwtKeyAutoGenerationWarningService>();
        }

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
    /// builder.Services.AddOptions&lt;JwtOptions&gt;()
    ///     .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    ///     .Validate(options => options.Key != null &amp;&amp; options.Key.Length >= 32,
    ///         "JWT signing key must be at least 32 bytes.")
    ///     .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer),
    ///         "JWT Issuer is required.")
    ///     .Validate(options => !string.IsNullOrWhiteSpace(options.Audience),
    ///         "JWT Audience is required.")
    ///     .ValidateOnStart();
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

    /// <summary>
    /// Background service that logs a warning if JWT options were auto-generated in non-production environments.
    /// </summary>
    private class JwtKeyAutoGenerationWarningService(
        ILogger<JwtKeyAutoGenerationWarningService> logger,
        IConfiguration configuration,
        IHostEnvironment environment) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Check what was configured in appsettings
            var configSection = configuration.GetSection(JwtOptions.SectionName);
            var keyFromConfig = configSection.GetValue<string>("Key");
            var issuerFromConfig = configSection.GetValue<string>("Issuer");
            var audienceFromConfig = configSection.GetValue<string>("Audience");
            
            var autoGenerated = new List<string>();
            
            if (string.IsNullOrWhiteSpace(keyFromConfig))
            {
                autoGenerated.Add("Key");
            }
            
            if (string.IsNullOrWhiteSpace(issuerFromConfig))
            {
                autoGenerated.Add($"Issuer (using 'NuxtIdentity.{environment.EnvironmentName}')");
            }
            
            if (string.IsNullOrWhiteSpace(audienceFromConfig))
            {
                autoGenerated.Add($"Audience (using 'NuxtIdentity.{environment.EnvironmentName}')");
            }
            
            if (autoGenerated.Count > 0)
            {
                logger.LogWarning(
                    "JWT configuration auto-generated for non-production environment: {AutoGenerated}. " +
                    "This is acceptable for Development and Testing, but tokens will not persist across restarts. " +
                    "For Production, configure Jwt:Key, Jwt:Issuer, and Jwt:Audience in appsettings.json. " +
                    "Generate a key using: [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))",
                    string.Join(", ", autoGenerated));
            }
            
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    #region Logger Messages

    [LoggerMessage(1, LogLevel.Warning, "{Location}: Authentication failed {RemoteIp}")]
    private static partial void LogJwtAuthenticationFailed(ILogger logger, Exception exception, string remoteIp, [CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Information, "{Location}: Challenge {Error} {ErrorDescription} {Path}")]
    private static partial void LogJwtChallenge(ILogger logger, string error, string errorDescription, string path, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Debug, "{Location}: Token validated {Username}")]
    private static partial void LogJwtTokenValidated(ILogger logger, string username, [CallerMemberName] string? location = null);

    #endregion
}

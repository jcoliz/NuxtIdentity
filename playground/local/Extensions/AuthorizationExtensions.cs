using Microsoft.AspNetCore.Authorization;
using NuxtIdentity.Playground.Local.Authorization;

namespace NuxtIdentity.Playground.Local.Extensions;

/// <summary>
/// Extension methods for configuring authorization.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Adds application authorization policies and handlers.
    /// </summary>
    public static IServiceCollection AddApplicationAuthorization(this IServiceCollection services)
    {
        // Add authorization with custom policies
        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireActiveSubscription", policy =>
                policy.Requirements.Add(new SubscriptionRequirement()));
        });

        // Register the authorization handler
        services.AddSingleton<IAuthorizationHandler, SubscriptionHandler>();

        // Register HttpContextAccessor (needed by the handler)
        services.AddHttpContextAccessor();

        return services;
    }
}
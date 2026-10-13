using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NuxtIdentity.AspNetCore.Services;
using NuxtIdentity.Core.Abstractions;

namespace NuxtIdentity.AspNetCore.Extensions;

/// <summary>
/// Service registration extensions for Identity seeding.
/// </summary>
public static class IdentitySeederServiceCollectionExtensions
{
    /// <summary>
    /// Registers the configuration-driven Identity seeder.
    /// </summary>
    /// <typeparam name="TUser">The application's Identity user type.</typeparam>
    /// <param name="services">The service collection to add the seeder to.</param>
    /// <param name="configuration">The application configuration containing the <c>IdentitySeeder</c> section.</param>
    /// <returns>The service collection so additional calls can be chained.</returns>
    public static IServiceCollection AddNuxtIdentitySeeding<TUser>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TUser : IdentityUser
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddScoped<IdentitySeeder<TUser>>(serviceProvider =>
            ActivatorUtilities.CreateInstance<IdentitySeeder<TUser>>(serviceProvider, configuration));

        return services;
    }

    /// <summary>
    /// Registers a custom seed stage that seeds application data from the same configured groups.
    /// </summary>
    /// <typeparam name="TStage">The stage implementation.</typeparam>
    /// <param name="services">The service collection to add the stage to.</param>
    /// <returns>The service collection so additional calls can be chained.</returns>
    /// <remarks>
    /// The stage is registered as scoped and resolved in the seeder's scope. Registering the
    /// same stage type more than once has no additional effect.
    /// </remarks>
    public static IServiceCollection AddSeedStage<TStage>(this IServiceCollection services)
        where TStage : class, ISeedStage
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISeedStage, TStage>());

        return services;
    }
}

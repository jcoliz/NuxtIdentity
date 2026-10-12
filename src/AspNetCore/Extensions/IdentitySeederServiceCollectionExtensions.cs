using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuxtIdentity.AspNetCore.Services;

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
}

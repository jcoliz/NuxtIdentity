using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NuxtIdentity.AspNetCore.Services;

namespace NuxtIdentity.AspNetCore.Extensions;

/// <summary>
/// Application startup extensions for Identity seeding.
/// </summary>
public static class IdentitySeederApplicationBuilderExtensions
{
    /// <summary>
    /// Runs Identity seeding in an asynchronous dependency-injection scope.
    /// </summary>
    /// <typeparam name="TUser">The application's Identity user type.</typeparam>
    /// <param name="applicationBuilder">The application builder that provides the root service provider.</param>
    /// <param name="cancellationToken">A token used to cancel seeding.</param>
    /// <returns>A task that completes when the configured seed operations have been attempted.</returns>
    /// <remarks>
    /// Call this after the application has created or migrated its database and
    /// before the application begins serving requests.
    /// </remarks>
    public static async Task SeedNuxtIdentityAsync<TUser>(
        this IApplicationBuilder applicationBuilder,
        CancellationToken cancellationToken = default)
        where TUser : IdentityUser
    {
        ArgumentNullException.ThrowIfNull(applicationBuilder);

        await using var scope = applicationBuilder.ApplicationServices.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder<TUser>>();
        await seeder.SeedAsync(cancellationToken);
    }
}

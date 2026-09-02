using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NuxtIdentity.AspNetCore.Extensions;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Configuration;
using NuxtIdentity.AspNetCore.Services;
using NuxtIdentity.EntityFrameworkCore.Extensions;
using System.Reflection;

namespace NuxtIdentity.AspNetCore.Tests.Helpers;

/// <summary>
/// Test web application factory configured for invitation-only registration mode.
/// Uses <see cref="InvitationOnlyTestAuthController"/> instead of <see cref="TestAuthController"/>.
/// </summary>
public class InvitationOnlyTestWebApplicationFactory : WebApplicationFactory<TestProgram>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration if any
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<TestDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Create and open SQLite connection (must stay open for in-memory database)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add SQLite in-memory database for testing
            services.AddDbContext<TestDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Configure Identity with TestUser
            services.AddIdentity<TestUser, IdentityRole>(options =>
            {
                // Disable password requirements for testing
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 1;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;

                // Disable user validation for testing
                options.User.RequireUniqueEmail = false;
            })
            .AddEntityFrameworkStores<TestDbContext>()
            .AddDefaultTokenProviders();

            // Configure JWT options
            services.Configure<JwtOptions>(options =>
            {
                var testOptions = TestJwtOptions.Create();
                options.Key = testOptions.Key;
                options.Issuer = testOptions.Issuer;
                options.Audience = testOptions.Audience;
                options.Lifespan = testOptions.Lifespan;
            });

            // Add NuxtIdentity services
            services.AddNuxtIdentity<TestUser>();
            services.AddNuxtIdentityAuthentication();

            // Register InMemoryUserNotifier for testing password reset flows
            var testNotifier = new InMemoryUserNotifier();
            services.AddSingleton<InMemoryUserNotifier>(testNotifier);
            services.AddSingleton<IUserNotifier>(testNotifier);

            // Add EF Core refresh token service
            services.AddNuxtIdentityEntityFramework<TestDbContext>();

            // Add controllers — use InvitationOnlyTestAuthController only (exclude TestAuthController)
            services.AddControllers()
                .AddApplicationPart(typeof(InvitationOnlyTestAuthController).Assembly)
                .ConfigureApplicationPartManager(manager =>
                {
                    manager.FeatureProviders.Add(
                        new ExcludeControllerFeatureProvider(typeof(TestAuthController)));
                });

            // Build service provider and create database
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<TestDbContext>();

            // Ensure database is created
            db.Database.EnsureCreated();

            // Seed Identity roles for invitation role assignment tests
            var roleManager = scopedServices.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var roleName in new[] { "Admin", "User" })
            {
                if (!roleManager.RoleExistsAsync(roleName).GetAwaiter().GetResult())
                {
                    roleManager.CreateAsync(new IdentityRole(roleName)).GetAwaiter().GetResult();
                }
            }
        });

        // Configure the test host
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Close();
            _connection?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Feature provider that excludes a specific controller type from MVC controller discovery.
/// </summary>
/// <param name="excludedType">The controller type to exclude.</param>
internal class ExcludeControllerFeatureProvider(Type excludedType) : IApplicationFeatureProvider<ControllerFeature>
{
    /// <summary>
    /// Removes the excluded controller type from the discovered controllers.
    /// </summary>
    /// <param name="parts">The application parts.</param>
    /// <param name="feature">The controller feature being populated.</param>
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        var toRemove = feature.Controllers
            .Where(c => c.AsType() == excludedType)
            .ToList();

        foreach (var controller in toRemove)
        {
            feature.Controllers.Remove(controller);
        }
    }
}

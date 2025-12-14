using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NuxtIdentity.AspNetCore.Extensions;
using NuxtIdentity.Core.Configuration;
using NuxtIdentity.EntityFrameworkCore.Extensions;

namespace NuxtIdentity.AspNetCore.Tests.Helpers;

/// <summary>
/// Test web application factory for integration testing with in-memory database.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<TestProgram>
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

            // Add EF Core refresh token service
            services.AddNuxtIdentityEntityFramework<TestDbContext>();

            // Add controllers
            services.AddControllers()
                .AddApplicationPart(typeof(TestAuthController).Assembly);

            // Build service provider and create database
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<TestDbContext>();

            // Ensure database is created
            db.Database.EnsureCreated();
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


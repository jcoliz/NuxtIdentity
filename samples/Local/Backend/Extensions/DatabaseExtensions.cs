using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Samples.Local.Data;

namespace NuxtIdentity.Samples.Local.Extensions;

/// <summary>
/// Extension methods for database configuration and seeding.
/// </summary>
public static class DatabaseExtensions
{
    /// <summary>
    /// Adds and configures the database context.
    /// </summary>
    public static IServiceCollection AddApplicationDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection") 
                ?? "Data Source=app.db"));
        
        return services;
    }
    
    /// <summary>
    /// Ensures the database is created and seeds initial data.
    /// </summary>
    public static async Task<IApplicationBuilder> EnsureDatabaseSetupAsync(
        this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var services = scope.ServiceProvider;
        
        var db = services.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("DatabaseSetup");
        
        return app;
    }
}
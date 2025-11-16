using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Playground.Local.Data;

namespace NuxtIdentity.Playground.Local.Extensions;

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
        
        await SeedRolesAsync(roleManager, logger);
        await SeedAdminUserAsync(userManager, logger);
        
        return app;
    }
    
    /// <summary>
    /// Seeds the application roles if they don't exist.
    /// </summary>
    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        string[] roles = ["guest", "account", "admin"];
        
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Created role: {Role}", role);
            }
        }
    }
    
    /// <summary>
    /// Seeds the default admin user if it doesn't exist.
    /// </summary>
    private static async Task SeedAdminUserAsync(UserManager<IdentityUser> userManager, ILogger logger)
    {
        const string adminUsername = "admin";
        const string adminPassword = "Admin123!";
        const string adminEmail = "admin@nuxtidentity.local";
        
        var adminUser = await userManager.FindByNameAsync(adminUsername);
        
        if (adminUser == null)
        {
            adminUser = new IdentityUser
            {
                UserName = adminUsername,
                Email = adminEmail,
                EmailConfirmed = true
            };
            
            var result = await userManager.CreateAsync(adminUser, adminPassword);
            
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "admin");
                logger.LogInformation("Created admin user: {Username} with password: {Password}", adminUsername, adminPassword);
                logger.LogWarning("IMPORTANT: Change the admin password in production!");
            }
            else
            {
                logger.LogError("Failed to create admin user: {Errors}", 
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
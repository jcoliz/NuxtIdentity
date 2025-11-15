using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.AspNetCore.Extensions;
using NuxtIdentity.EntityFrameworkCore.Extensions;
using NuxtIdentity.Core.Configuration;
using NuxtIdentity.Playground.Local.Data;
using NuxtIdentity.Playground.Local.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Configure JWT options
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Data Source=app.db"));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings (you can adjust these)
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    
    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Add NuxtIdentity
builder.Services.AddNuxtIdentity<ApplicationUser>();
builder.Services.AddNuxtIdentityEntityFramework<ApplicationDbContext>();
builder.Services.AddNuxtIdentityAuthentication();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add NSwag services
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "NuxtIdentity Playground API";
    config.Version = "v1";
    
    // Add JWT security definition
    config.AddSecurity("Bearer", new NSwag.OpenApiSecurityScheme
    {
        Type = NSwag.OpenApiSecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT token (without 'Bearer' prefix)"
    });
    
    // Make all endpoints require the Bearer token by default
    config.OperationProcessors.Add(new NSwag.Generation.Processors.Security.AspNetCoreOperationSecurityScopeProcessor("Bearer"));

});

var app = builder.Build();

// Ensure database is created and seed roles and admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
    
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    await SeedRolesAsync(roleManager, logger);
    await SeedAdminUserAsync(userManager, logger);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Add NSwag middleware
    app.UseOpenApi();
    app.UseSwaggerUi();
}
else
{
    app.UseHttpsRedirection();
}

// Enable CORS
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>
/// Seeds the application roles if they don't exist.
/// </summary>
static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
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
static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager, ILogger logger)
{
    const string adminUsername = "admin";
    const string adminPassword = "Admin123!";
    const string adminEmail = "admin@nuxtidentity.local";
    
    var adminUser = await userManager.FindByNameAsync(adminUsername);
    
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminUsername,
            Email = adminEmail,
            DisplayName = "Administrator",
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

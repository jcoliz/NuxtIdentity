using Microsoft.AspNetCore.Identity;
using NuxtIdentity.AspNetCore.Extensions;
using NuxtIdentity.EntityFrameworkCore.Extensions;
using NuxtIdentity.Core.Configuration;
using NuxtIdentity.Playground.Local.Data;
using NuxtIdentity.Playground.Local.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Configure JWT options
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

// Add Database
builder.Services.AddApplicationDatabase(builder.Configuration);

// Add Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Add NuxtIdentity
builder.Services.AddNuxtIdentity<IdentityUser>();
builder.Services.AddNuxtIdentityEntityFramework<ApplicationDbContext>();
builder.Services.AddNuxtIdentityAuthentication();

// Register ApplicationDbContext as IDbContextCleaner
builder.Services.AddScoped<NuxtIdentity.Core.Abstractions.IDbContextCleaner>(sp =>
    sp.GetRequiredService<ApplicationDbContext>());

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

    config.AddSecurity("Bearer", new NSwag.OpenApiSecurityScheme
    {
        Type = NSwag.OpenApiSecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT token (without 'Bearer' prefix)"
    });

    config.OperationProcessors.Add(new NSwag.Generation.Processors.Security.AspNetCoreOperationSecurityScopeProcessor("Bearer"));
});

// Add application authorization
//builder.Services.AddApplicationAuthorization();
// TODO: I fear this is a build break. This file must
// be sitting around locally on one of my machines!!

var app = builder.Build();

// Ensure database is created and seeded
await app.EnsureDatabaseSetupAsync();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

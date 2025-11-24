using Microsoft.AspNetCore.Identity;
using NuxtIdentity.AspNetCore.Extensions;
using NuxtIdentity.EntityFrameworkCore.Extensions;
using NuxtIdentity.Core.Configuration;
using NuxtIdentity.Samples.Local.Data;
using NuxtIdentity.Samples.Local.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

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
builder.Services.AddNuxtIdentityWithEntityFramework<IdentityUser, ApplicationDbContext>(
    builder.Configuration);

// Add NSwag services
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "swagger";
    config.Title = "Weather Forecast API";
    config.Version = "v1";
    config.Description = "A simple ASP.NET Core Web API for weather forecasts";

    config.AddSecurity("Bearer", new NSwag.OpenApiSecurityScheme
    {
        Type = NSwag.OpenApiSecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT token (without 'Bearer' prefix)"
    });
    
    config.OperationProcessors.Add(new NSwag.Generation.Processors.Security.AspNetCoreOperationSecurityScopeProcessor("Bearer"));

});

var app = builder.Build();

// Ensure database is created
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

app.UseExceptionHandler();  // This enables ProblemDetails for exceptions
app.UseStatusCodePages(); // This enables ProblemDetails for status codes like 404

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapControllers();

app.Run();

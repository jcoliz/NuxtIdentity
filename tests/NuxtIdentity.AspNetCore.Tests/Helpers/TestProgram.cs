using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace NuxtIdentity.AspNetCore.Tests.Helpers;

/// <summary>
/// Minimal test application program class for WebApplicationFactory.
/// This provides the entry point required by WebApplicationFactory while
/// allowing the factory to override configuration in ConfigureWebHost.
/// </summary>
public class TestProgram
{
    public static void Main(string[] args)
    {
        var options = new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        };

        var builder = WebApplication.CreateBuilder(options);

        // Basic services - will be overridden in TestWebApplicationFactory
        builder.Services.AddControllers();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();

        var app = builder.Build();

        // Basic middleware pipeline
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using NuxtIdentity.AspNetCore.Configuration;

namespace NuxtIdentity.AspNetCore.Extensions;

public static class NuxtIdentityServiceCollectionExtensions
{
    public static IServiceCollection AddNuxtIdentityAuthentication(
        this IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer();
        
        services.ConfigureOptions<JwtBearerOptionsSetup>();
        
        return services;
    }
}
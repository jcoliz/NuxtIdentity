# TODOs

Prompt: "Please have a look at the implementation. Is there anything else that should go in the entityframework libary?"

Looking at the current implementation, the **NuxtIdentity.EntityFrameworkCore** library looks solid! It has:

✅ **Already included:**
- `EfRefreshTokenService<TContext>` - Generic refresh token service
- `ConfigureNuxtIdentityRefreshTokens()` - ModelBuilder extension
- `AddNuxtIdentityEntityFramework<TContext>()` - Service registration

**Potential additions to consider:**

1. **Background cleanup service** - A hosted service to periodically clean up expired/revoked tokens:

````csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.EntityFrameworkCore.Services;

/// <summary>
/// Background service that periodically cleans up expired and revoked refresh tokens.
/// </summary>
/// <typeparam name="TContext">The DbContext type that contains RefreshTokens DbSet.</typeparam>
public partial class RefreshTokenCleanupService<TContext> : BackgroundService
    where TContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RefreshTokenCleanupService<TContext>> _logger;
    private readonly TimeSpan _cleanupInterval;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshTokenCleanupService{TContext}"/> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider for creating scopes.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="cleanupInterval">How often to run cleanup (default: 1 hour).</param>
    public RefreshTokenCleanupService(
        IServiceProvider serviceProvider,
        ILogger<RefreshTokenCleanupService<TContext>> logger,
        TimeSpan? cleanupInterval = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _cleanupInterval = cleanupInterval ?? TimeSpan.FromHours(1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogCleanupServiceStarted(_cleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
                await CleanupExpiredTokensAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                LogCleanupError(ex);
            }
        }

        LogCleanupServiceStopped();
    }

    private async Task CleanupExpiredTokensAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        LogCleanupStarted();

        var cutoffDate = DateTime.UtcNow;
        var expiredTokens = await context.Set<RefreshTokenEntity>()
            .Where(t => t.ExpiresAt < cutoffDate || t.IsRevoked)
            .ToListAsync(cancellationToken);

        if (expiredTokens.Count > 0)
        {
            context.Set<RefreshTokenEntity>().RemoveRange(expiredTokens);
            await context.SaveChangesAsync(cancellationToken);
            LogCleanupCompleted(expiredTokens.Count);
        }
        else
        {
            LogNoTokensToCleanup();
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Refresh token cleanup service started. Interval: {interval}")]
    private partial void LogCleanupServiceStarted(TimeSpan interval);

    [LoggerMessage(Level = LogLevel.Information, Message = "Refresh token cleanup service stopped")]
    private partial void LogCleanupServiceStopped();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting refresh token cleanup")]
    private partial void LogCleanupStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "Cleaned up {count} expired/revoked refresh tokens")]
    private partial void LogCleanupCompleted(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No tokens to cleanup")]
    private partial void LogNoTokensToCleanup();

    [LoggerMessage(Level = LogLevel.Error, Message = "Error during refresh token cleanup")]
    private partial void LogCleanupError(Exception exception);

    #endregion
}
````

2. **Update ServiceCollectionExtensions** to optionally add the cleanup service:

````csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.EntityFrameworkCore.Services;

namespace NuxtIdentity.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering NuxtIdentity Entity Framework Core services.
/// </summary>
public static class NuxtIdentityServiceCollectionExtensions
{
    /// <summary>
    /// Adds NuxtIdentity Entity Framework Core services to the dependency injection container.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type that contains RefreshTokens DbSet.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="enableCleanupService">Whether to enable automatic cleanup of expired tokens (default: true).</param>
    /// <param name="cleanupInterval">How often to run cleanup (default: 1 hour).</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registers:
    /// - <see cref="EfRefreshTokenService{TContext}"/> as <see cref="IRefreshTokenService"/>
    /// - <see cref="RefreshTokenCleanupService{TContext}"/> as a hosted service (optional)
    /// 
    /// Your DbContext must have a DbSet&lt;RefreshTokenEntity&gt; configured and should call
    /// modelBuilder.ConfigureNuxtIdentityRefreshTokens() in OnModelCreating.
    /// 
    /// Example usage:
    /// <code>
    /// services.AddDbContext&lt;ApplicationDbContext&gt;(options => ...);
    /// services.AddNuxtIdentityEntityFramework&lt;ApplicationDbContext&gt;();
    /// </code>
    /// </remarks>
    public static IServiceCollection AddNuxtIdentityEntityFramework<TContext>(
        this IServiceCollection services,
        bool enableCleanupService = true,
        TimeSpan? cleanupInterval = null)
        where TContext : DbContext
    {
        services.AddScoped<IRefreshTokenService, EfRefreshTokenService<TContext>>();
        
        if (enableCleanupService)
        {
            services.AddSingleton<RefreshTokenCleanupService<TContext>>(sp =>
                new RefreshTokenCleanupService<TContext>(
                    sp,
                    sp.GetRequiredService<ILogger<RefreshTokenCleanupService<TContext>>>(),
                    cleanupInterval));
            
            services.AddHostedService(sp => 
                sp.GetRequiredService<RefreshTokenCleanupService<TContext>>());
        }
        
        return services;
    }
}
````

**However**, if you want to keep the library minimal and focused, the current implementation is already **complete and production-ready**. The cleanup service is nice-to-have but not essential since:
- Expired tokens are already checked during validation
- Revoked tokens are marked as `IsRevoked` and won't validate
- Database size growth is typically slow (unless you have millions of users)

**My recommendation**: Keep the current implementation as-is for now. It's clean, focused, and complete. You can always add the cleanup service later as an enhancement if database size becomes a concern.

The library is ready to use! 🎉
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Configuration;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.EntityFrameworkCore.Services;

/// <summary>
/// Entity Framework Core implementation of refresh token service.
/// </summary>
/// <typeparam name="TContext">The DbContext type that contains RefreshTokens DbSet.</typeparam>
/// <remarks>
/// This implementation stores refresh tokens in a database using Entity Framework Core.
/// Tokens are hashed using SHA256 before storage for security.
///
/// The DbContext must have a DbSet&lt;RefreshTokenEntity&gt; configured. You can add this
/// to your context like:
/// <code>
/// public DbSet&lt;RefreshTokenEntity&gt; RefreshTokens =&gt; Set&lt;RefreshTokenEntity&gt;();
/// </code>
/// </remarks>
public partial class EfRefreshTokenService<TContext> : IRefreshTokenService
    where TContext : DbContext
{
    private readonly TContext _context;
    private readonly ILogger<EfRefreshTokenService<TContext>> _logger;
    private readonly JwtOptions _jwtOptions;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _revokedTokenLifespan = TimeSpan.FromDays(7);

    /// <summary>
    /// Initializes a new instance of the <see cref="EfRefreshTokenService{TContext}"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="jwtOptions">JWT configuration options.</param>
    /// <param name="timeProvider">Time provider for testable time operations. Defaults to system time if not provided.</param>
    public EfRefreshTokenService(
        TContext context,
        ILogger<EfRefreshTokenService<TContext>> logger,
        IOptions<JwtOptions> jwtOptions,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _logger = logger;
        _jwtOptions = jwtOptions.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateRefreshTokenAsync(string userId)
    {
        LogStartingUserId(userId);

        var token = GenerateSecureToken();
        var tokenHash = HashToken(token);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var entity = new RefreshTokenEntity
        {
            TokenHash = tokenHash,
            UserId = userId,
            ExpiresAt = now.Add(_jwtOptions.RefreshTokenLifespan),
            CreatedAt = now,
            IsRevoked = false
        };

        _context.Set<RefreshTokenEntity>().Add(entity);
        await _context.SaveChangesAsync();

        // Await cleanup of expired tokens to ensure completion before returning
        // We must await to avoid concurrency problems
        try
        {
            await DeleteExpiredTokensAsync();
        }
        catch (Exception ex)
        {
            LogCleanupFailed(ex);
        }

        LogTokenGenerated(token, userId);

        LogOkUserId(userId);
        return token;
    }

    /// <inheritdoc/>
    public async Task<string?> ValidateRefreshTokenAsync(string token)
    {
        LogStarting();

        var tokenHash = HashToken(token);

        var entity = await _context.Set<RefreshTokenEntity>()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (entity == null)
        {
            LogTokenNotFoundForValidation(token);
            return null;
        }

        if (entity.IsRevoked)
        {
            LogTokenInvalidBecauseRevoked(token,entity.UserId);
            return null;
        }

        if (entity.ExpiresAt < _timeProvider.GetUtcNow().UtcDateTime)
        {
            LogTokenExpired(token,entity.UserId, entity.ExpiresAt);
            return null;
        }

        LogTokenValidated(token, entity.UserId);

        LogOkUserId(entity.UserId);
        return entity.UserId;
    }

    /// <inheritdoc/>
    public async Task RevokeRefreshTokenAsync(string token)
    {
        LogStarting();

        var tokenHash = HashToken(token);

        var entity = await _context.Set<RefreshTokenEntity>()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (entity != null)
        {
            entity.IsRevoked = true;
            entity.ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.Add(_revokedTokenLifespan);
            await _context.SaveChangesAsync();

            LogTokenRevoked(token, entity.UserId);

            LogOk();
        }
        else
        {
            LogTokenNotFoundForRevocation(token);
        }
    }

    /// <inheritdoc/>
    public async Task RevokeAllUserTokensAsync(string userId)
    {
        LogStartingUserId(userId);

        var userTokens = await _context.Set<RefreshTokenEntity>()
            .Where(t => t.UserId == userId)
            .ToListAsync();

        var expirationDate = _timeProvider.GetUtcNow().UtcDateTime.Add(_revokedTokenLifespan);
        foreach (var token in userTokens)
        {
            token.IsRevoked = true;
            token.ExpiresAt = expirationDate;
        }

        await _context.SaveChangesAsync();

        LogOkUserIdCount(userId, userTokens.Count);
    }

    /// <summary>
    /// Deletes all expired refresh tokens from the database.
    /// </summary>
    /// <remarks>
    /// This method should be called periodically (e.g., via a background job) to clean up
    /// expired tokens and maintain database hygiene. It removes tokens that are past their expiration date.
    /// </remarks>
    /// <returns>The number of tokens deleted.</returns>
    private async Task<int> DeleteExpiredTokensAsync()
    {
        LogStarting();

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var tokensToDelete = await _context.Set<RefreshTokenEntity>()
            .Where(t => t.ExpiresAt < now)
            .ToListAsync();

        if (tokensToDelete.Count > 0)
        {
            _context.Set<RefreshTokenEntity>().RemoveRange(tokensToDelete);
            await _context.SaveChangesAsync();

            LogOkCount(tokensToDelete.Count);
        }
        else
        {
            LogOk();
        }

        return tokensToDelete.Count;
    }

    /// <summary>
    /// Generates a cryptographically secure random token.
    /// </summary>
    /// <returns>A base64-encoded random token.</returns>
    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Hashes a token using SHA256.
    /// </summary>
    /// <param name="token">The token to hash.</param>
    /// <returns>A base64-encoded hash of the token.</returns>
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    #region Logger Messages

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Debug, "{Location}: Starting {UserId}")]
    private partial void LogStartingUserId(string userId, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Information, "{Location}: OK")]
    private partial void LogOk([CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Information, "{Location}: OK {UserId}")]
    private partial void LogOkUserId(string userId, [CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Information, "{Location}: OK {UserId} {Count}")]
    private partial void LogOkUserIdCount(string userId, int count, [CallerMemberName] string? location = null);

    [LoggerMessage(6, LogLevel.Information, "{Location}: OK {Count}")]
    private partial void LogOkCount(int count, [CallerMemberName] string? location = null);

    [LoggerMessage(7, LogLevel.Warning, "{Location}: Token {token} not found {UserId}")]
    private partial void LogTokenNotFound(string token, string userId, [CallerMemberName] string? location = null);

    [LoggerMessage(8, LogLevel.Warning, "{Location}: Token {token} is invalid {UserId}, because it was revoked")]
    private partial void LogTokenInvalidBecauseRevoked(string token, string userId, [CallerMemberName] string? location = null);

    [LoggerMessage(9, LogLevel.Warning, "{Location}: Token {token} expired {UserId} {ExpiresAt}")]
    private partial void LogTokenExpired(string token, string userId, DateTime expiresAt, [CallerMemberName] string? location = null);

    [LoggerMessage(10, LogLevel.Warning, "{Location}: Token {token} not found for revocation")]
    private partial void LogTokenNotFoundForRevocation(string token, [CallerMemberName] string? location = null);

    [LoggerMessage(11, LogLevel.Warning, "{Location}: Cleanup failed")]
    private partial void LogCleanupFailed(Exception ex, [CallerMemberName] string? location = null);

    [LoggerMessage(12, LogLevel.Warning, "{Location}: Token {token} not found for validation")]
    private partial void LogTokenNotFoundForValidation(string token, [CallerMemberName] string? location = null);

    [LoggerMessage(13, LogLevel.Debug, "{Location}: Token {token} generated for {UserId}")]
    private partial void LogTokenGenerated(string token, string userId, [CallerMemberName] string? location = null);

    [LoggerMessage(14, LogLevel.Debug, "{Location}: Token {token} validated for {UserId}")]
    private partial void LogTokenValidated(string token, string userId, [CallerMemberName] string? location = null);

    [LoggerMessage(15, LogLevel.Debug, "{Location}: Token {token} revoked for {UserId}")]
    private partial void LogTokenRevoked(string token, string userId, [CallerMemberName] string? location = null);

    #endregion
}

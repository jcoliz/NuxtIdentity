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

        var key = Guid.NewGuid();
        var secret = GenerateSecureToken();
        var tokenHash = HashToken(secret);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var entity = new RefreshTokenEntity
        {
            Key = key,
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

        LogOkTokenUserId(userId, key);
        return FormatToken(key, secret);
    }

    /// <inheritdoc/>
    public async Task<string?> ValidateRefreshTokenAsync(string token)
    {
        LogStarting();

        if (!TryParseToken(token, out var key, out var secret))
        {
            LogOldFormatToken();
            return null;
        }

        var secretHash = HashToken(secret);

        var entity = await _context.Set<RefreshTokenEntity>()
            .FirstOrDefaultAsync(t => t.Key == key);

        if (entity == null)
        {
            LogTokenKeyNotFound(key);
            return null;
        }

        if (entity.TokenHash != secretHash)
        {
            LogTokenSecretMismatch(key, entity.UserId);
            return null;
        }

        if (entity.IsRevoked)
        {
            LogTokenInvalidBecauseRevoked(key, entity.UserId);
            return null;
        }

        if (entity.ExpiresAt < _timeProvider.GetUtcNow().UtcDateTime)
        {
            LogTokenExpired(key, entity.UserId, entity.ExpiresAt);
            return null;
        }

        LogOkTokenUserId(entity.UserId, key);
        return entity.UserId;
    }

    /// <inheritdoc/>
    public async Task RevokeRefreshTokenAsync(string token)
    {
        LogStarting();

        if (!TryParseToken(token, out var key, out _))
        {
            LogOldFormatToken();
            return;
        }

        var entity = await _context.Set<RefreshTokenEntity>()
            .FirstOrDefaultAsync(t => t.Key == key);

        if (entity != null)
        {
            entity.IsRevoked = true;
            entity.ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.Add(_revokedTokenLifespan);
            await _context.SaveChangesAsync();

            LogOkTokenUserId(entity.UserId, key);
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

    /// <summary>
    /// Formats a token key and secret into the composite token string.
    /// </summary>
    /// <param name="key">The token key GUID.</param>
    /// <param name="secret">The token secret.</param>
    /// <returns>The formatted token string in the format {Key}.{Secret}.</returns>
    private static string FormatToken(Guid key, string secret)
    {
        return $"{key}.{secret}";
    }

    /// <summary>
    /// Attempts to parse a composite token into its key and secret components.
    /// </summary>
    /// <param name="token">The composite token string.</param>
    /// <param name="key">The parsed GUID key.</param>
    /// <param name="secret">The parsed secret portion.</param>
    /// <returns>True if parsing succeeded; false for old-format or invalid tokens.</returns>
    private static bool TryParseToken(string token, out Guid key, out string secret)
    {
        key = Guid.Empty;
        secret = string.Empty;

        if (string.IsNullOrEmpty(token))
            return false;

        // GUID is 36 characters (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)
        // Token format: {Key}.{Secret}
        var dotIndex = token.IndexOf('.');
        if (dotIndex != 36)
            return false;

        var keyPart = token.AsSpan(0, 36);
        if (!Guid.TryParse(keyPart, out key))
            return false;

        secret = token[(dotIndex + 1)..];
        return !string.IsNullOrEmpty(secret);
    }

    #region Logger Messages

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Debug, "{Location}: Starting User {UserId}")]
    private partial void LogStartingUserId(string userId, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Information, "{Location}: OK")]
    private partial void LogOk([CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Information, "{Location}: OK User {UserId} Count {Count}")]
    private partial void LogOkUserIdCount(string userId, int count, [CallerMemberName] string? location = null);

    [LoggerMessage(6, LogLevel.Information, "{Location}: OK Count {Count}")]
    private partial void LogOkCount(int count, [CallerMemberName] string? location = null);

    [LoggerMessage(11, LogLevel.Warning, "{Location}: Cleanup failed")]
    private partial void LogCleanupFailed(Exception ex, [CallerMemberName] string? location = null);

    // New log messages for key-based token operations (event IDs 16+)

    [LoggerMessage(16, LogLevel.Information, "{Location}: OK Token {TokenKey} for {UserId}")]
    private partial void LogOkTokenUserId(string userId, Guid tokenKey, [CallerMemberName] string? location = null);

    [LoggerMessage(17, LogLevel.Warning, "{Location}: Token {TokenKey} secret mismatch {UserId}")]
    private partial void LogTokenSecretMismatch(Guid tokenKey, string userId, [CallerMemberName] string? location = null);

    [LoggerMessage(18, LogLevel.Warning, "{Location}: Token {TokenKey} is invalid {UserId}, because it was revoked")]
    private partial void LogTokenInvalidBecauseRevoked(Guid tokenKey, string userId, [CallerMemberName] string? location = null);

    [LoggerMessage(19, LogLevel.Warning, "{Location}: Token {TokenKey} expired {UserId} {ExpiresAt}")]
    private partial void LogTokenExpired(Guid tokenKey, string userId, DateTime expiresAt, [CallerMemberName] string? location = null);

    [LoggerMessage(20, LogLevel.Warning, "{Location}: Token {TokenKey} not found")]
    private partial void LogTokenKeyNotFound(Guid tokenKey, [CallerMemberName] string? location = null);

    [LoggerMessage(21, LogLevel.Debug, "{Location}: Old-format token received")]
    private partial void LogOldFormatToken([CallerMemberName] string? location = null);

    #endregion
}

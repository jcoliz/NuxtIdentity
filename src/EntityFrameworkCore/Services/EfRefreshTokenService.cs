using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NuxtIdentity.Core.Abstractions;
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
    private const int RefreshTokenExpirationDays = 30;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfRefreshTokenService{TContext}"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="logger">Logger instance.</param>
    public EfRefreshTokenService(TContext context, ILogger<EfRefreshTokenService<TContext>> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateRefreshTokenAsync(string userId)
    {
        var token = GenerateSecureToken();
        var tokenHash = HashToken(token);

        var entity = new RefreshTokenEntity
        {
            TokenHash = tokenHash,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        _context.Set<RefreshTokenEntity>().Add(entity);
        await _context.SaveChangesAsync();

        LogTokenGenerated(userId, entity.ExpiresAt, token);

        return token;
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateRefreshTokenAsync(string token, string userId)
    {
        var tokenHash = HashToken(token);

        var entity = await _context.Set<RefreshTokenEntity>()
            .FirstOrDefaultAsync(t =>
                t.TokenHash == tokenHash &&
                t.UserId == userId);

        if (entity == null)
        {
            LogTokenNotFound(userId);
            return false;
        }

        if (entity.IsRevoked)
        {
            LogTokenRevoked(userId,token);
            return false;
        }

        if (entity.ExpiresAt < DateTime.UtcNow)
        {
            LogTokenExpired(userId, token, entity.ExpiresAt);
            return false;
        }

        LogTokenValid(userId, token);
        return true;
    }

    /// <inheritdoc/>
    public async Task RevokeRefreshTokenAsync(string token)
    {
        var tokenHash = HashToken(token);

        var entity = await _context.Set<RefreshTokenEntity>()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (entity != null)
        {
            entity.IsRevoked = true;
            await _context.SaveChangesAsync();
        }
        else
        {
            LogTokenNotFoundForRevocation();
        }
    }

    /// <inheritdoc/>
    public async Task RevokeAllUserTokensAsync(string userId)
    {
        LogRevokingAllUserTokens(userId);

        var userTokens = await _context.Set<RefreshTokenEntity>()
            .Where(t => t.UserId == userId)
            .ToListAsync();

        foreach (var token in userTokens)
        {
            token.IsRevoked = true;
        }

        await _context.SaveChangesAsync();

        LogAllUserTokensRevoked(userId, userTokens.Count);
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generating refresh token for user: {userId}")]
    private partial void LogGeneratingToken(string userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Refresh token generated for user: {userId}, expires: {expiresAt}, token: {token}")]
    private partial void LogTokenGenerated(string userId, DateTime expiresAt, string token);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Validating refresh token for user: {userId} token: {token}")]
    private partial void LogValidatingToken(string userId, string token);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token: Validation failed, not found for user: {userId}")]
    private partial void LogTokenNotFound(string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token: Validation failed, revoked for user {userId} token: {token}")]
    private partial void LogTokenRevoked(string userId, string token);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token: Validation failed, token {token} expired for user: {userId} at {expiresAt}")]
    private partial void LogTokenExpired(string userId, string token,DateTime expiresAt);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Refresh token: Valid for user: {userId}, token: {token}")]
    private partial void LogTokenValid(string userId, string token);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Revoking refresh token: {token}")]
    private partial void LogRevokingToken(string token);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token revoked for user: {userId}")]
    private partial void LogTokenRevokedForUser(string userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token not found for revocation")]
    private partial void LogTokenNotFoundForRevocation();

    [LoggerMessage(Level = LogLevel.Information, Message = "Revoking all refresh tokens for user: {userId}")]
    private partial void LogRevokingAllUserTokens(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Revoked {count} tokens for user: {userId}")]
    private partial void LogAllUserTokensRevoked(string userId, int count);

    #endregion
}

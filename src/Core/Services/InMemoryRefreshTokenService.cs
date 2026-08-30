using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Configuration;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.Core.Services;

/// <summary>
/// In-memory implementation of refresh token service using collections.
/// </summary>
public class InMemoryRefreshTokenService : IRefreshTokenService
{
    private readonly List<RefreshTokenEntity> _tokens = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JwtOptions _jwtOptions;
    private readonly TimeProvider _timeProvider;
    private int _nextId = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryRefreshTokenService"/> class.
    /// </summary>
    /// <param name="jwtOptions">JWT configuration options.</param>
    /// <param name="timeProvider">Time provider for testable time operations. Defaults to system time if not provided.</param>
    public InMemoryRefreshTokenService(IOptions<JwtOptions> jwtOptions, TimeProvider? timeProvider = null)
    {
        _jwtOptions = jwtOptions.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateRefreshTokenAsync(string userId)
    {
        var key = Guid.NewGuid();
        var secret = GenerateSecureToken();
        var tokenHash = HashToken(secret);

        await _lock.WaitAsync();
        try
        {
            var now = _timeProvider.GetUtcNow().DateTime;
            var entity = new RefreshTokenEntity
            {
                Id = _nextId++,
                Key = key,
                TokenHash = tokenHash,
                UserId = userId,
                ExpiresAt = now.Add(_jwtOptions.RefreshTokenLifespan),
                CreatedAt = now,
                IsRevoked = false
            };

            _tokens.Add(entity);
            return FormatToken(key, secret);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<string?> ValidateRefreshTokenAsync(string token)
    {
        if (!TryParseToken(token, out var key, out var secret))
            return null;

        var secretHash = HashToken(secret);

        await _lock.WaitAsync();
        try
        {
            var entity = _tokens.FirstOrDefault(t => t.Key == key);

            if (entity == null)
                return null;

            if (entity.TokenHash != secretHash)
                return null;

            if (entity.IsRevoked)
                return null;

            if (entity.ExpiresAt < _timeProvider.GetUtcNow().DateTime)
                return null;

            return entity.UserId;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RevokeRefreshTokenAsync(string token)
    {
        if (!TryParseToken(token, out var key, out _))
            return;

        await _lock.WaitAsync();
        try
        {
            var entity = _tokens.FirstOrDefault(t => t.Key == key);
            if (entity != null)
            {
                entity.IsRevoked = true;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RevokeAllUserTokensAsync(string userId)
    {
        await _lock.WaitAsync();
        try
        {
            var userTokens = _tokens.Where(t => t.UserId == userId);
            foreach (var token in userTokens)
            {
                token.IsRevoked = true;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RecentUserLogin>> GetUsersLoggedInRecentlyAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return _tokens
                .GroupBy(t => t.UserId)
                .Select(group => new RecentUserLogin(
                    group.Key,
                    group.Max(t => t.CreatedAt)))
                .OrderByDescending(login => login.LastLoginAt)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
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

    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}

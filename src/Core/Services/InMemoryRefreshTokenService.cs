using System.Security.Cryptography;
using System.Text;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.Core.Services;

/// <summary>
/// In-memory implementation of refresh token service using collections.
/// </summary>
public class InMemoryRefreshTokenService : IRefreshTokenService
{
    private readonly List<RefreshTokenEntity> _tokens = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int _nextId = 1;
    private const int RefreshTokenExpirationDays = 30;

    /// <inheritdoc/>
    public async Task<string> GenerateRefreshTokenAsync(string userId)
    {
        var token = GenerateSecureToken();
        var tokenHash = HashToken(token);

        await _lock.WaitAsync();
        try
        {
            var entity = new RefreshTokenEntity
            {
                Id = _nextId++,
                TokenHash = tokenHash,
                UserId = userId,
                ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            };

            _tokens.Add(entity);
            return token;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateRefreshTokenAsync(string token, string userId)
    {
        var tokenHash = HashToken(token);

        await _lock.WaitAsync();
        try
        {
            var entity = _tokens.FirstOrDefault(t => 
                t.TokenHash == tokenHash && 
                t.UserId == userId);

            if (entity == null)
                return false;

            if (entity.IsRevoked)
                return false;

            if (entity.ExpiresAt < DateTime.UtcNow)
                return false;

            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RevokeRefreshTokenAsync(string token)
    {
        var tokenHash = HashToken(token);

        await _lock.WaitAsync();
        try
        {
            var entity = _tokens.FirstOrDefault(t => t.TokenHash == tokenHash);
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

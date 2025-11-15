using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Playground.Local.Data;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.Playground.Local.Services;

/// <summary>
/// Entity Framework implementation of refresh token service.
/// </summary>
public class EfRefreshTokenService : IRefreshTokenService
{
    private readonly ApplicationDbContext _context;
    private const int RefreshTokenExpirationDays = 30;

    public EfRefreshTokenService(ApplicationDbContext context)
    {
        _context = context;
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

        _context.RefreshTokens.Add(entity);
        await _context.SaveChangesAsync();
        
        return token;
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateRefreshTokenAsync(string token, string userId)
    {
        var tokenHash = HashToken(token);

        var entity = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => 
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

    /// <inheritdoc/>
    public async Task RevokeRefreshTokenAsync(string token)
    {
        var tokenHash = HashToken(token);

        var entity = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        
        if (entity != null)
        {
            entity.IsRevoked = true;
            await _context.SaveChangesAsync();
        }
    }

    /// <inheritdoc/>
    public async Task RevokeAllUserTokensAsync(string userId)
    {
        var userTokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId)
            .ToListAsync();

        foreach (var token in userTokens)
        {
            token.IsRevoked = true;
        }

        await _context.SaveChangesAsync();
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
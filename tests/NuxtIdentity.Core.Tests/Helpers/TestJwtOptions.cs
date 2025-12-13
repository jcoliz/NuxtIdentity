using NuxtIdentity.Core.Configuration;
using System.Text;

namespace NuxtIdentity.Core.Tests.Helpers;

/// <summary>
/// Provides pre-configured JWT options for testing.
/// </summary>
public static class TestJwtOptions
{
    /// <summary>
    /// Creates default JWT options for testing.
    /// </summary>
    public static JwtOptions CreateDefault()
    {
        return new JwtOptions
        {
            Key = Encoding.UTF8.GetBytes("ThisIsASecretKeyForTestingPurposesOnly1234567890"),
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            Lifespan = TimeSpan.FromMinutes(15),
            RefreshTokenLifespan = TimeSpan.FromDays(7)
        };
    }

    /// <summary>
    /// Creates JWT options with a short lifespan for expiration testing.
    /// </summary>
    public static JwtOptions CreateShortLivedToken()
    {
        var options = CreateDefault();
        options.Lifespan = TimeSpan.FromMilliseconds(100);
        return options;
    }

    /// <summary>
    /// Creates JWT options with a short refresh token lifespan for expiration testing.
    /// </summary>
    public static JwtOptions CreateShortLivedRefreshToken()
    {
        var options = CreateDefault();
        options.RefreshTokenLifespan = TimeSpan.FromMilliseconds(100);
        return options;
    }

    /// <summary>
    /// Creates JWT options with a different key for tampering tests.
    /// </summary>
    public static JwtOptions CreateWithDifferentKey()
    {
        var options = CreateDefault();
        options.Key = Encoding.UTF8.GetBytes("DifferentSecretKeyForTestingPurposesOnly1234567890");
        return options;
    }
}

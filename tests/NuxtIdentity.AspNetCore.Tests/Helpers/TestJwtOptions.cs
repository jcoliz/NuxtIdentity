using System.Text;
using NuxtIdentity.Core.Configuration;

namespace NuxtIdentity.AspNetCore.Tests.Helpers;

/// <summary>
/// Provides test JWT options with a valid configuration.
/// </summary>
public static class TestJwtOptions
{
    /// <summary>
    /// Creates a valid JWT options instance for testing.
    /// </summary>
    public static JwtOptions Create()
    {
        // Generate a 32-byte key for HMAC-SHA256
        var key = Encoding.UTF8.GetBytes("ThisIsATestKeyThatIs32BytesLong!");

        return new JwtOptions
        {
            Key = key,
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            Lifespan = TimeSpan.FromMinutes(15)
        };
    }
}

using NuxtIdentity.Core.Abstractions;
using System.Security.Claims;

namespace NuxtIdentity.Core.Tests.Helpers;

/// <summary>
/// Simple claims provider for test users.
/// </summary>
public class TestUserClaimsProvider : IUserClaimsProvider<TestUser>
{
    public Task<IEnumerable<Claim>> GetClaimsAsync(TestUser user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email)
        };

        return Task.FromResult<IEnumerable<Claim>>(claims);
    }
}

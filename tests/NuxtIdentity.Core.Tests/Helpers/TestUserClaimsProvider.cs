using NuxtIdentity.Core.Abstractions;
using System.IdentityModel.Tokens.Jwt;
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
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Name, user.Username),
        };

        return Task.FromResult<IEnumerable<Claim>>(claims);
    }
}

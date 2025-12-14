using Microsoft.AspNetCore.Identity;

namespace NuxtIdentity.AspNetCore.Tests.Helpers;

/// <summary>
/// Test user implementation for testing purposes.
/// </summary>
public class TestUser : IdentityUser
{
    public TestUser()
    {
    }

    public TestUser(string userName) : base(userName)
    {
        Email = $"{userName}@test.com";
    }
}

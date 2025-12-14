using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NuxtIdentity.AspNetCore.Controllers;
using NuxtIdentity.Core.Abstractions;

namespace NuxtIdentity.AspNetCore.Tests.Helpers;

/// <summary>
/// Concrete implementation of NuxtAuthControllerBase for testing.
/// </summary>
public class TestAuthController : NuxtAuthControllerBase<TestUser>
{
    public TestAuthController(
        IJwtTokenService<TestUser> jwtTokenService,
        IRefreshTokenService refreshTokenService,
        UserManager<TestUser> userManager,
        SignInManager<TestUser> signInManager,
        ILogger<TestAuthController> logger)
        : base(jwtTokenService, refreshTokenService, userManager, signInManager, logger)
    {
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NuxtIdentity.AspNetCore.Controllers;
using NuxtIdentity.Core.Abstractions;

namespace NuxtIdentity.AspNetCore.Tests.Helpers;

/// <summary>
/// Concrete implementation of NuxtAuthControllerBase for testing.
/// </summary>
public class TestAuthController(
    IJwtTokenService<TestUser> jwtTokenService,
    IEnumerable<IUserClaimsProvider<TestUser>> claimsProviders,
    IRefreshTokenService refreshTokenService,
    UserManager<TestUser> userManager,
    SignInManager<TestUser> signInManager,
    IEnumerable<IUserNotifier<TestUser>> userNotifiers,
    ILogger<TestAuthController> logger)
    : NuxtAuthControllerBase<TestUser>(
        jwtTokenService,
        claimsProviders,
        refreshTokenService,
        userManager,
        signInManager,
        userNotifiers,
        logger)
{
}

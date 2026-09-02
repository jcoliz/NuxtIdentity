using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NuxtIdentity.AspNetCore.Controllers;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.AspNetCore.Tests.Helpers;

/// <summary>
/// Test auth controller that enforces invitation-only registration mode.
/// </summary>
public class InvitationOnlyTestAuthController(
    IJwtTokenService<TestUser> jwtTokenService,
    IEnumerable<IUserClaimsProvider<TestUser>> claimsProviders,
    IRefreshTokenService refreshTokenService,
    UserManager<TestUser> userManager,
    SignInManager<TestUser> signInManager,
    IEnumerable<IUserNotifier> userNotifiers,
    IEnumerable<IInvitationService> invitationServices,
    ILogger<InvitationOnlyTestAuthController> logger)
    : NuxtAuthControllerBase<TestUser>(
        jwtTokenService,
        claimsProviders,
        refreshTokenService,
        userManager,
        signInManager,
        userNotifiers,
        invitationServices,
        logger)
{
    /// <inheritdoc/>
    protected override RegistrationOptions RegistrationOptions =>
        new(RegistrationMode.InvitationOnly);
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using NuxtIdentity.AspNetCore.Controllers;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;
using System.Text.Json;

namespace NuxtIdentity.Samples.Local.Controllers;

/// <summary>
/// Authentication controller for the NuxtIdentity playground.
/// </summary>
/// <remarks>
/// This controller demonstrates the minimal implementation needed when using
/// NuxtAuthControllerBase. It simply provides a concrete class that ASP.NET Core
/// can instantiate and map routes to.
/// 
/// The base controller provides complete implementations for all endpoints:
/// - POST /api/auth/login - Username/password authentication
/// - POST /api/auth/signup - User registration
/// - GET /api/auth/user - Get current user session with roles and claims
/// - POST /api/auth/refresh - Token refresh with rotation
/// - POST /api/auth/logout - Token revocation
/// 
/// All endpoints can be overridden if custom behavior is needed, but the defaults
/// work well for most ASP.NET Core Identity scenarios.
/// </remarks>
public class AuthController(
    IJwtTokenService<IdentityUser> jwtTokenService,
    IRefreshTokenService refreshTokenService,
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    ILogger<AuthController> logger) 
    : NuxtAuthControllerBase<IdentityUser>(
        jwtTokenService, 
        refreshTokenService, 
        userManager,
        signInManager,
        logger)
{
    // No overrides needed! The base controller provides complete functionality.
}

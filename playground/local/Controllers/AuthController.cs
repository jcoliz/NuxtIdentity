using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using NuxtIdentity.AspNetCore.Controllers;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;
using NuxtIdentity.Playground.Local.Models;
using NuxtIdentity.Playground.Local.Constants;
using System.Text.Json;

namespace NuxtIdentity.Playground.Local.Controllers;

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
    IJwtTokenService<ApplicationUser> jwtTokenService,
    IRefreshTokenService refreshTokenService,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<AuthController> logger) 
    : NuxtAuthControllerBase<ApplicationUser>(
        jwtTokenService, 
        refreshTokenService, 
        userManager,
        signInManager,
        logger)
{
    // No overrides needed! The base controller provides complete functionality.
}

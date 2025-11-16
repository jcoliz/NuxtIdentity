using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using NuxtIdentity.AspNetCore.Controllers;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.Samples.Local.Backend.Controllers;

/// <summary>
/// Authentication controller for the NuxtIdentity sample backend.
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
    /// <summary>
    /// Override signup to assign guest role to new users.
    /// </summary>
    public override async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
    {
        // Call the base implementation first
        var result = await base.SignUp(request);
        
        // If signup was successful, assign the guest role
        if (result is OkObjectResult okResult && okResult.Value is LoginResponse loginResponse)
        {
            var user = await UserManager.FindByEmailAsync(request.Email);
            if (user != null)
            {
                await UserManager.AddToRoleAsync(user, "guest");
            }
        }
        
        return result;
    }
}

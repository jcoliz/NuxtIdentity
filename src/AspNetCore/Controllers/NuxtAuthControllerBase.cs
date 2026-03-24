using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Exceptions;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.AspNetCore.Controllers;

/// <summary>
/// Base controller for NuxtIdentity authentication endpoints with ASP.NET Core Identity integration.
/// </summary>
/// <typeparam name="TUser">The type of user this controller works with. Must inherit from IdentityUser.</typeparam>
/// <remarks>
/// <para><strong>Partial Class File Map:</strong></para>
/// <para>
/// This class is split across multiple files for maintainability:
/// </para>
/// <list type="bullet">
/// <item><description><c>NuxtAuthControllerBase.cs</c> (this file) — Class declaration, properties, helper methods, hooks</description></item>
/// <item><description><c>NuxtAuthControllerBase.Auth.cs</c> — Authentication endpoints: Login, SignUp, GetSession, RefreshTokens, Logout</description></item>
/// <item><description><c>NuxtAuthControllerBase.Password.cs</c> — Password management: ForgotPassword, ResetPassword, ChangePassword, Base64URL encoding</description></item>
/// <item><description><c>NuxtAuthControllerBase.Log.cs</c> — All LoggerMessage declarations (IDs 1–21)</description></item>
/// </list>
///
/// <para>
/// This base controller provides complete authentication endpoints that work with ASP.NET Core Identity:
/// - Login: Username/password authentication
/// - SignUp: User registration
/// - Session: Get current user information
/// - Refresh: Token refresh with rotation
/// - Logout: Token revocation
/// </para>
///
/// <para><strong>Default Behavior:</strong></para>
/// <para>
/// All endpoints have sensible default implementations that work with standard IdentityUser.
/// The defaults handle:
/// - User authentication via SignInManager
/// - User creation via UserManager
/// - Role and claim extraction from Identity
/// - Token generation and validation
/// </para>
///
/// <para><strong>Customization:</strong></para>
/// <para>
/// All endpoint methods are virtual and can be overridden for custom behavior.
/// Common scenarios for overriding:
/// - Custom user properties (extend IdentityUser)
/// - Additional validation logic
/// - Email verification requirements
/// - Multi-factor authentication
/// - Custom response formats
/// </para>
///
/// <para><strong>User Information Mapping:</strong></para>
/// <para>
/// The controller automatically maps ASP.NET Core Identity data to the UserInfo model:
/// - Id: User.Id
/// - Name: User.UserName
/// - Email: User.Email
/// - Roles: All roles assigned to the user
/// - Claims: All user claims and role claims
/// </para>
/// </remarks>
[ApiController]
[Route("api/auth")]
public abstract partial class NuxtAuthControllerBase<TUser>(
    IJwtTokenService<TUser> jwtTokenService,
    IEnumerable<IUserClaimsProvider<TUser>> claimsProviders,
    IRefreshTokenService refreshTokenService,
    UserManager<TUser> userManager,
    SignInManager<TUser> signInManager,
    IEnumerable<IUserNotifier<TUser>> userNotifiers,
    IEnumerable<IInvitationService> invitationServices,
    ILogger logger) : ControllerBase
    where TUser : IdentityUser, new()
{
    /// <summary>
    /// Gets the JWT token service for generating and validating tokens.
    /// </summary>
    protected IJwtTokenService<TUser> JwtTokenService { get; } = jwtTokenService;

    /// <summary>
    /// Gets the refresh token service for managing refresh tokens.
    /// </summary>
    protected IRefreshTokenService RefreshTokenService { get; } = refreshTokenService;

    /// <summary>
    /// Gets the user manager for Identity operations.
    /// </summary>
    protected UserManager<TUser> UserManager { get; } = userManager;

    /// <summary>
    /// Gets the sign-in manager for authentication operations.
    /// </summary>
    protected SignInManager<TUser> SignInManager { get; } = signInManager;

    /// <summary>
    /// Gets all registered user notifiers for sending notifications (e.g., email, audit log).
    /// </summary>
    protected IEnumerable<IUserNotifier<TUser>> UserNotifiers { get; } = userNotifiers;

    /// <summary>
    /// Gets the invitation service, or null if none is registered.
    /// </summary>
    /// <exception cref="NuxtIdentityConfigurationException">
    /// Thrown during construction if more than one <see cref="IInvitationService"/> is registered.
    /// </exception>
    protected IInvitationService? InvitationService { get; } = invitationServices.Count() switch
    {
        0 => null,
        1 => invitationServices.First(),
        _ => throw new NuxtIdentityConfigurationException(
            nameof(IInvitationService),
            "Multiple IInvitationService implementations registered. Only one invitation store is supported.")
    };

    /// <summary>
    /// Gets the registration options controlling how user registration behaves.
    /// Override in derived controllers to change registration mode (e.g., <see cref="RegistrationMode.InvitationOnly"/>).
    /// </summary>
    protected virtual RegistrationOptions RegistrationOptions => new();

    #region Helper Methods

    /// <summary>
    /// Creates a login response with tokens and user information.
    /// </summary>
    /// <param name="user">The authenticated user.</param>
    /// <returns>A login response containing tokens and user data.</returns>
    protected async Task<LoginResponse> CreateLoginResponseAsync(TUser user)
    {
        var accessToken = await JwtTokenService.GenerateAccessTokenAsync(user);
        var refreshToken = await RefreshTokenService.GenerateRefreshTokenAsync(user.Id);
        var userInfo = await CreateUserInfoAsync(user);

        return new LoginResponse
        {
            Token = new TokenPair
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            },
            User = userInfo
        };
    }

    /// <summary>
    /// Creates a refresh response with new tokens.
    /// </summary>
    /// <param name="user">The user to generate tokens for.</param>
    /// <param name="oldRefreshToken">The old refresh token to revoke.</param>
    /// <returns>A refresh response containing new token pair.</returns>
    protected async Task<RefreshResponse> CreateRefreshResponseAsync(
        TUser user,
        string oldRefreshToken)
    {
        LogStartingUsername(user.UserName ?? "unknown");

        // Revoke old token (token rotation)
        await RefreshTokenService.RevokeRefreshTokenAsync(oldRefreshToken);

        var newAccessToken = await JwtTokenService.GenerateAccessTokenAsync(user);
        var newRefreshToken = await RefreshTokenService.GenerateRefreshTokenAsync(user.Id);

        LogOkUsername(user.UserName ?? "unknown");

        return new RefreshResponse
        {
            Token = new TokenPair
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            }
        };
    }

    /// <summary>
    /// Creates a UserInfo object from an IdentityUser with roles and claims.
    /// </summary>
    /// <param name="user">The user to create info for.</param>
    /// <returns>UserInfo populated with user data, roles, and claims.</returns>
    protected virtual async Task<UserInfo> CreateUserInfoAsync(TUser user)
    {
        var roles = await UserManager.GetRolesAsync(user);
        var userClaims = await UserManager.GetClaimsAsync(user);

        var providedClaims = new List<Claim>();
        foreach (var provider in claimsProviders)
        {
            if (provider is Services.IdentityUserClaimsProvider<TUser> identityProvider)
            {
                // Only get the stored claims, not the identity claims
                var identityClaims = await identityProvider.GetStoredClaimsAsync(user);
                providedClaims.AddRange(identityClaims);
            }
            else
            {
                var providerClaims = await provider.GetClaimsAsync(user);
                providedClaims.AddRange(providerClaims);                                
            }
        }

        var allClaims = userClaims
            .Concat(providedClaims)
            .Select(c => new ClaimInfo { Type = c.Type, Value = c.Value })
            .Distinct()
            .ToArray();

        return new UserInfo
        {
            Id = user.Id,
            Name = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Roles = roles.ToArray(),
            Claims = allClaims
        };
    }

    /// <summary>
    /// Gets the current user's ID from the claims principal.
    /// </summary>
    /// <returns>The user ID if authenticated; otherwise, null.</returns>
    protected string? GetCurrentUserId()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    /// Gets the current user's name from the claims principal.
    /// </summary>
    /// <returns>The username if authenticated; otherwise, null.</returns>
    protected string? GetCurrentUsername()
    {
        return User.Identity?.Name;
    }

    /// <summary>
    /// Gets a user by their ID. Can be overridden for custom user lookup logic.
    /// </summary>
    /// <param name="userId">The user ID to look up.</param>
    /// <returns>The user if found; otherwise, null.</returns>
    protected virtual async Task<TUser?> GetUserByIdAsync(string userId)
    {
        return await UserManager.FindByIdAsync(userId);
    }

    /// <summary>
    /// Finds a user by username or email address.
    /// </summary>
    /// <param name="username">The username to search for, or null.</param>
    /// <param name="email">The email address to search for, or null.</param>
    /// <returns>The user if found; otherwise, null.</returns>
    private async Task<TUser?> FindUserByUsernameOrEmailAsync(string? username, string? email)
    {
        if (!string.IsNullOrEmpty(username))
            return await UserManager.FindByNameAsync(username);

        if (!string.IsNullOrEmpty(email))
            return await UserManager.FindByEmailAsync(email);

        return null;
    }

    #endregion

    #region Hooks

    /// <summary>
    /// Hook method called after a user is created. Can be overridden for custom logic.
    /// </summary>
    /// <param name="user">The newly created user.</param>
    protected virtual Task OnUserCreatedAsync(TUser user)
    {
        // Hook for derived classes to implement custom logic after user creation
        return Task.CompletedTask;
    }

    /// <summary>
    /// Hook method called after a user signs up via invitation with roles and claims assigned.
    /// Can be overridden for custom post-invitation-acceptance logic.
    /// </summary>
    /// <param name="user">The newly created user.</param>
    /// <param name="invitation">The invitation that was accepted.</param>
    protected virtual Task OnInvitationAcceptedAsync(TUser user, InvitationEntity invitation)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Hook method called after a user's email is confirmed. Added as a placeholder for Phase 3.
    /// Can be overridden for custom post-confirmation logic.
    /// </summary>
    /// <param name="user">The user whose email was confirmed.</param>
    protected virtual Task OnUserConfirmedAsync(TUser user)
    {
        return Task.CompletedTask;
    }

    #endregion
}

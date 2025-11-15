using System.Security.Claims;

namespace NuxtIdentity.Playground.Local.Services;

/// <summary>
/// Provides claims for a user to be included in JWT tokens.
/// </summary>
/// <typeparam name="TUser">The type of user.</typeparam>
/// <remarks>
/// This interface abstracts the process of extracting claims from a user object, enabling
/// the JWT token service to remain agnostic about the specific user type and how to retrieve
/// user information (roles, permissions, custom claims, etc.).
/// 
/// By separating claim extraction from token generation, we achieve several design goals:
/// 
/// 1. **Technology Independence**: The core JWT service doesn't need to know about ASP.NET
///    Core Identity, Entity Framework, or any other specific technology. Different implementations
///    of this interface can use different user stores (Identity, custom database, external API).
/// 
/// 2. **Customization**: Applications can easily customize which claims are included in tokens
///    by providing their own implementation without modifying the core token generation logic.
/// 
/// 3. **Testability**: Mock implementations can be easily created for testing without requiring
///    a full user management system.
/// 
/// 4. **Library Packaging**: The core library (NuxtIdentity.Core) can include the interface,
///    while technology-specific implementations live in separate packages (e.g., 
///    NuxtIdentity.Identity for ASP.NET Core Identity, NuxtIdentity.Custom for custom implementations).
/// 
/// Standard claims typically include: NameIdentifier, Name, Email, Role, and JWT-specific claims
/// like Sub (subject) and Jti (JWT ID for token uniqueness).
/// </remarks>
public interface IUserClaimsProvider<TUser> where TUser : class
{
    /// <summary>
    /// Gets the claims for the specified user.
    /// </summary>
    /// <param name="user">The user to get claims for.</param>
    /// <returns>A collection of claims for the user.</returns>
    Task<IEnumerable<Claim>> GetClaimsAsync(TUser user);
}
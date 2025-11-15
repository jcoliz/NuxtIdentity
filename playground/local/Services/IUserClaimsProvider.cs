using System.Security.Claims;

namespace NuxtIdentity.Playground.Local.Services;

/// <summary>
/// Provides claims for a user to be included in JWT tokens.
/// </summary>
/// <typeparam name="TUser">The type of user.</typeparam>
public interface IUserClaimsProvider<TUser> where TUser : class
{
    /// <summary>
    /// Gets the claims for the specified user.
    /// </summary>
    /// <param name="user">The user to get claims for.</param>
    /// <returns>A collection of claims for the user.</returns>
    Task<IEnumerable<Claim>> GetClaimsAsync(TUser user);
}
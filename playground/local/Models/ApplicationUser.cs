using Microsoft.AspNetCore.Identity;

namespace NuxtIdentity.Playground.Local.Models;

/// <summary>
/// Application user extending Identity user with custom properties.
/// </summary>
/// <remarks>
/// TODO: Remove this. We should just use IdentityUser for this simple example
/// </remarks>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Gets or sets the user's display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
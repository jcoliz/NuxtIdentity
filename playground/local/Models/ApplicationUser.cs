using Microsoft.AspNetCore.Identity;

namespace NuxtIdentity.Playground.Local.Models;

/// <summary>
/// Application user extending Identity user with custom properties.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Gets or sets the user's display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
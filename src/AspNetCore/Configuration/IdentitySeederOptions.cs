namespace NuxtIdentity.AspNetCore.Configuration;

/// <summary>
/// Configuration for one named Identity seed group.
/// </summary>
public class IdentitySeederGroupOptions
{
    /// <summary>
    /// Gets or sets the group's ordering priority. Lower values are processed first.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the roles to seed.
    /// </summary>
    public List<SeedRoleOptions> Roles { get; set; } = new();

    /// <summary>
    /// Gets or sets the users to seed.
    /// </summary>
    public List<SeedUserOptions> Users { get; set; } = new();

    /// <summary>
    /// Gets or sets the role claims to seed.
    /// </summary>
    public List<SeedRoleClaimsOptions> RoleClaims { get; set; } = new();

    /// <summary>
    /// Gets or sets the invitations to seed.
    /// </summary>
    public List<SeedInvitationOptions> Invitations { get; set; } = new();

    /// <summary>
    /// Gets the name of this group, populated from its configuration key.
    /// </summary>
    internal string? GroupName { get; set; }

    /// <summary>
    /// Gets the configuration path for this group.
    /// </summary>
    internal string? ConfigurationPath { get; set; }
}

/// <summary>
/// Configuration for a role to seed.
/// </summary>
public class SeedRoleOptions
{
    /// <summary>
    /// Gets or sets the optional GUID to use when creating the role.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the role name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets the configuration path for this role.
    /// </summary>
    internal string? ConfigurationPath { get; set; }
}

/// <summary>
/// Configuration for a user to seed.
/// </summary>
public class SeedUserOptions
{
    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the optional email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the optional password for a newly created user.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets whether the email address is confirmed. Defaults to true.
    /// </summary>
    public bool EmailConfirmed { get; set; } = true;

    /// <summary>
    /// Gets or sets the role names to assign to this user.
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// Gets or sets the claims to add to this user.
    /// </summary>
    public List<SeedClaimOptions> Claims { get; set; } = new();

    /// <summary>
    /// Gets the configuration path for this user.
    /// </summary>
    internal string? ConfigurationPath { get; set; }
}

/// <summary>
/// Configuration for a claim to seed.
/// </summary>
public class SeedClaimOptions
{
    /// <summary>
    /// Gets or sets the claim type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the claim value.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Gets the configuration path for this claim.
    /// </summary>
    internal string? ConfigurationPath { get; set; }
}

/// <summary>
/// Configuration for claims to add to a role.
/// </summary>
public class SeedRoleClaimsOptions
{
    /// <summary>
    /// Gets or sets the name of the role that receives these claims.
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Gets or sets the claims to add to the role.
    /// </summary>
    public List<SeedClaimOptions> Claims { get; set; } = new();

    /// <summary>
    /// Gets the configuration path for this role-claims entry.
    /// </summary>
    internal string? ConfigurationPath { get; set; }
}

/// <summary>
/// Configuration for an invitation to seed.
/// </summary>
public class SeedInvitationOptions
{
    /// <summary>
    /// Gets or sets the invitation code in GUID format.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Gets or sets the optional email address for the invitee.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the roles granted when the invitation is claimed.
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// Gets or sets the claims granted when the invitation is claimed.
    /// </summary>
    public List<SeedClaimOptions> Claims { get; set; } = new();

    /// <summary>
    /// Gets or sets optional application-specific metadata.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the optional duration until the invitation expires.
    /// </summary>
    public TimeSpan? ExpiresIn { get; set; }

    /// <summary>
    /// Gets the configuration path for this invitation.
    /// </summary>
    internal string? ConfigurationPath { get; set; }
}

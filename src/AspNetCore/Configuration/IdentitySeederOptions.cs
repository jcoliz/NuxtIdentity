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
    /// Gets or sets the claims to seed for users identified by username.
    /// </summary>
    public List<SeedUserClaimsOptions> UserClaims { get; set; } = new();

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
    /// <remarks>
    /// The group name is not part of the group object's bound values, so the seeder
    /// retains it separately for deterministic ordering and structured diagnostics.
    /// </remarks>
    internal string? GroupName { get; set; }

    /// <summary>
    /// Gets the configuration path for this group.
    /// </summary>
    /// <remarks>
    /// The seeder keeps the source path so group-level binding errors can identify
    /// the relevant configuration section.
    /// </remarks>
    internal string? ConfigurationPath { get; set; }

    /// <summary>
    /// Gets or sets a group-level configuration parsing error.
    /// </summary>
    /// <remarks>
    /// Invalid scalar values are retained with the bound group so execution can
    /// report the error and continue processing other groups.
    /// </remarks>
    internal string? ConfigurationError { get; set; }
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
    /// <remarks>
    /// The seeder uses the path to report invalid role configuration without
    /// needing to reconstruct array indices.
    /// </remarks>
    internal string? ConfigurationPath { get; set; }

    /// <summary>
    /// Gets or sets a role-level configuration parsing error.
    /// </summary>
    /// <remarks>
    /// This allows malformed role data to be isolated and reported during seeding
    /// without preventing valid roles from being processed.
    /// </remarks>
    internal string? ConfigurationError { get; set; }
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
    /// Gets the configuration paths corresponding to <see cref="Roles"/>.
    /// </summary>
    /// <remarks>
    /// Role names alone do not retain their individual configuration keys. The
    /// seeder keeps these paths aligned with <see cref="Roles"/> so assignment
    /// failures can point to the exact configured entry.
    /// </remarks>
    internal List<string> RoleConfigurationPaths { get; } = new();

    /// <summary>
    /// Gets or sets the claims to add to this user.
    /// </summary>
    public List<SeedClaimOptions> Claims { get; set; } = new();

    /// <summary>
    /// Gets the configuration path for this user.
    /// </summary>
    /// <remarks>
    /// The seeder retains this path to identify malformed user configuration and
    /// provide useful diagnostics for operations associated with the user.
    /// </remarks>
    internal string? ConfigurationPath { get; set; }

    /// <summary>
    /// Gets or sets a user-level configuration parsing error.
    /// </summary>
    /// <remarks>
    /// Keeping parsing errors with the user allows that item to be reported and
    /// skipped independently from unrelated users and seed operations.
    /// </remarks>
    internal string? ConfigurationError { get; set; }
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
    /// <remarks>
    /// The seeder retains the path so invalid claims can be reported at their
    /// individual configuration entry.
    /// </remarks>
    internal string? ConfigurationPath { get; set; }

    /// <summary>
    /// Gets or sets a claim-level configuration parsing error.
    /// </summary>
    /// <remarks>
    /// Claim errors are stored per item so one malformed claim does not prevent
    /// other claims from being processed.
    /// </remarks>
    internal string? ConfigurationError { get; set; }
}

/// <summary>
/// Configuration for claims to add to a user identified by username.
/// </summary>
public class SeedUserClaimsOptions
{
    /// <summary>
    /// Gets or sets the username of the user that receives these claims.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the claims to add to the user.
    /// </summary>
    public List<SeedClaimOptions> Claims { get; set; } = new();

    /// <summary>
    /// Gets the configuration path for this user-claims entry.
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
    /// <remarks>
    /// The seeder retains this path to report malformed role-claims entries with
    /// their original configuration location.
    /// </remarks>
    internal string? ConfigurationPath { get; set; }

    /// <summary>
    /// Gets or sets a role-claims-level configuration parsing error.
    /// </summary>
    /// <remarks>
    /// This lets the seeder isolate a malformed role-claims entry without
    /// suppressing other role claims or groups.
    /// </remarks>
    internal string? ConfigurationError { get; set; }
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
    /// <remarks>
    /// Invitation codes must not be included in diagnostics. The seeder retains
    /// this path to identify invitation configuration safely when reporting errors.
    /// </remarks>
    internal string? ConfigurationPath { get; set; }

    /// <summary>
    /// Gets or sets an invitation-level configuration parsing error.
    /// </summary>
    /// <remarks>
    /// This lets the seeder report malformed invitation data by path, without
    /// exposing its code or preventing other invitations from being processed.
    /// </remarks>
    internal string? ConfigurationError { get; set; }
}

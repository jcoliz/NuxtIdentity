namespace NuxtIdentity.Core.Abstractions;

/// <summary>
/// Seeds application-specific data from the same named configuration groups as
/// Identity seeding.
/// </summary>
/// <remarks>
/// <para>
/// The seeder calls each registered stage once per configured group, after the
/// built-in Identity stages for that group. Stages run in ascending
/// <see cref="Order"/>, then by ordinal <see cref="Name"/>.
/// </para>
/// <para>
/// Implementations must follow these rules, which the seeder cannot enforce:
/// </para>
/// <list type="bullet">
/// <item><description>
/// Create-only: match each item by a natural key, create it when absent, return
/// <see cref="SeedResult.AlreadyExists(string)"/> when present, and never update
/// or delete existing data.
/// </description></item>
/// <item><description>
/// No secrets in logs: item kinds, identifying keys, and <see cref="SeedResult"/>
/// reasons are logged. They must never contain passwords, invitation codes,
/// tokens, other secrets, or unsanitized external error text.
/// </description></item>
/// <item><description>
/// Reserved keys: do not define items under the built-in group keys
/// <c>Order</c>, <c>Roles</c>, <c>Users</c>, <c>UserClaims</c>,
/// <c>RoleClaims</c>, or <c>Invitations</c>.
/// </description></item>
/// <item><description>
/// Run every item through <see cref="ISeedGroupContext.RunAsync(string, string, string, Func{Task{SeedResult}})"/>
/// so its failures are isolated and counted.
/// </description></item>
/// </list>
/// </remarks>
public interface ISeedStage
{
    /// <summary>
    /// Gets the stable stage name used in logs and as the ordering tie-breaker.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the execution order among custom stages within a group.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Seeds this stage's items for one configured group.
    /// </summary>
    /// <param name="group">The group being seeded.</param>
    /// <param name="cancellationToken">A token used to cancel seeding.</param>
    Task SeedAsync(ISeedGroupContext group, CancellationToken cancellationToken);
}

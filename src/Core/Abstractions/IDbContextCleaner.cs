namespace NuxtIdentity.Core.Abstractions;

/// <summary>
/// Provides a mechanism to clear the Entity Framework DbContext change tracker.
/// </summary>
/// <remarks>
/// This interface allows NuxtIdentity to request change tracker clearing without
/// requiring a direct dependency on Entity Framework Core. This is useful for
/// preventing DbContext concurrency issues when the same context instance is used
/// for multiple operations in quick succession.
/// </remarks>
public interface IDbContextCleaner
{
    /// <summary>
    /// Clears the Entity Framework DbContext change tracker, stopping tracking of all entities.
    /// </summary>
    void ClearChangeTracker();
}

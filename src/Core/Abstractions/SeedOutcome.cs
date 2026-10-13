namespace NuxtIdentity.Core.Abstractions;

/// <summary>
/// The outcome category of a single seed operation.
/// </summary>
public enum SeedOutcome
{
    /// <summary>The item was created.</summary>
    Succeeded,

    /// <summary>The item could not be created.</summary>
    Failed,

    /// <summary>The item was not attempted, for example because a dependency is unavailable.</summary>
    Skipped,

    /// <summary>The item already exists and was left unchanged.</summary>
    AlreadyExists
}

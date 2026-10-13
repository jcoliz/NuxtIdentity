namespace NuxtIdentity.Core.Abstractions;

/// <summary>
/// The result of a single seed operation.
/// </summary>
/// <remarks>
/// Reasons are logged and must never contain secrets or unsanitized external error text.
/// </remarks>
public sealed class SeedResult
{
    private SeedResult(SeedOutcome outcome, string? reason)
    {
        Outcome = outcome;
        Reason = reason;
    }

    /// <summary>
    /// Gets the outcome category.
    /// </summary>
    public SeedOutcome Outcome { get; }

    /// <summary>
    /// Gets the logged reason for a non-success outcome.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// The item was created.
    /// </summary>
    public static SeedResult Success() => new(SeedOutcome.Succeeded, null);

    /// <summary>
    /// The item already exists and was left unchanged.
    /// </summary>
    public static SeedResult AlreadyExists(string reason) => new(SeedOutcome.AlreadyExists, reason);

    /// <summary>
    /// The item could not be created.
    /// </summary>
    public static SeedResult Failure(string reason) => new(SeedOutcome.Failed, reason);

    /// <summary>
    /// The item was not attempted.
    /// </summary>
    public static SeedResult Skipped(string reason) => new(SeedOutcome.Skipped, reason);
}

namespace NuxtIdentity.Core.Abstractions;

/// <summary>
/// Provides a custom seed stage with access to one configured seed group.
/// </summary>
public abstract class SeedGroupContext
{
    /// <summary>
    /// Gets the group name.
    /// </summary>
    public abstract string GroupName { get; }

    /// <summary>
    /// Gets the group's configuration path.
    /// </summary>
    public abstract string Path { get; }

    /// <summary>
    /// Binds each child of the group's <paramref name="key"/> subsection to <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// A binding failure affects only that item: it is returned with a default value and
    /// <see cref="SeedItem{T}.Error"/> set, and enumeration continues.
    /// </remarks>
    /// <param name="key">The group key containing the items, for example <c>Lists</c>.</param>
    public abstract IEnumerable<SeedItem<T>> Items<T>(string key) where T : new();

    /// <summary>
    /// Runs one item operation with failure isolation, outcome counting, and structured logging.
    /// </summary>
    /// <param name="itemKind">A short description of the item type, for example <c>list</c>. Logged.</param>
    /// <param name="path">The item's configuration path. Logged.</param>
    /// <param name="identifyingKey">A non-secret key identifying the item. Logged.</param>
    /// <param name="operation">The operation that creates the item when absent.</param>
    public abstract Task RunAsync(
        string itemKind,
        string path,
        string identifyingKey,
        Func<Task<SeedResult>> operation);

    /// <summary>
    /// Runs one operation for a bound item, recording a failure without invoking
    /// <paramref name="operation"/> when the item has a binding error.
    /// </summary>
    /// <param name="item">The bound item.</param>
    /// <param name="itemKind">A short description of the item type, for example <c>list</c>. Logged.</param>
    /// <param name="identifyingKey">A non-secret key identifying the item. Logged.</param>
    /// <param name="operation">The operation that creates the item when absent.</param>
    public Task RunAsync<T>(
        SeedItem<T> item,
        string itemKind,
        string identifyingKey,
        Func<Task<SeedResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(operation);

        var error = item.Error;
        return RunAsync(
            itemKind,
            item.Path,
            identifyingKey,
            error is null ? operation : () => Task.FromResult(SeedResult.Failure(error)));
    }

    /// <summary>
    /// Creates a bound item for implementations of <see cref="Items{T}(string)"/>.
    /// </summary>
    protected static SeedItem<T> CreateItem<T>(T value, string path, string? error) =>
        new(value, path, error);
}

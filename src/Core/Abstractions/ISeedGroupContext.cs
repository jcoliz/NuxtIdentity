namespace NuxtIdentity.Core.Abstractions;

/// <summary>
/// Provides a custom seed stage with access to one configured seed group.
/// </summary>
public interface ISeedGroupContext
{
    /// <summary>
    /// Gets the group name.
    /// </summary>
    string GroupName { get; }

    /// <summary>
    /// Gets the group's configuration path.
    /// </summary>
    string Path { get; }

    /// <summary>
    /// Binds each child of the group's <paramref name="key"/> subsection to <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// A binding failure affects only that item: it is returned with a default value and
    /// <see cref="SeedItem{T}.Error"/> set, and enumeration continues.
    /// </remarks>
    /// <param name="key">The group key containing the items, for example <c>Lists</c>.</param>
    IEnumerable<SeedItem<T>> Items<T>(string key) where T : new();

    /// <summary>
    /// Runs one item operation with failure isolation, outcome counting, and structured logging.
    /// </summary>
    /// <param name="itemKind">A short description of the item type, for example <c>list</c>. Logged.</param>
    /// <param name="path">The item's configuration path. Logged.</param>
    /// <param name="identifyingKey">A non-secret key identifying the item. Logged.</param>
    /// <param name="operation">The operation that creates the item when absent.</param>
    Task RunAsync(
        string itemKind,
        string path,
        string identifyingKey,
        Func<Task<SeedResult>> operation);

    /// <summary>
    /// Runs one operation for a bound item at the item's configuration path.
    /// </summary>
    /// <remarks>
    /// When the item has a binding error, <paramref name="operation"/> is not invoked and the
    /// item is recorded as failed with <see cref="SeedItem{T}.Error"/> as the reason.
    /// </remarks>
    /// <param name="item">The bound item.</param>
    /// <param name="itemKind">A short description of the item type, for example <c>list</c>. Logged.</param>
    /// <param name="identifyingKey">A non-secret key identifying the item. Logged.</param>
    /// <param name="operation">The operation that creates the item when absent.</param>
    Task RunAsync<T>(
        SeedItem<T> item,
        string itemKind,
        string identifyingKey,
        Func<Task<SeedResult>> operation);
}

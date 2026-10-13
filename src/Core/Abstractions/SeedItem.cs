namespace NuxtIdentity.Core.Abstractions;

/// <summary>
/// A configured seed item bound to a typed options object.
/// </summary>
/// <typeparam name="T">The options type the item is bound to.</typeparam>
public sealed class SeedItem<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SeedItem{T}"/> class.
    /// </summary>
    /// <param name="value">The bound value.</param>
    /// <param name="path">The configuration path of the item.</param>
    /// <param name="error">A safe description of a binding error, or <see langword="null"/>.</param>
    public SeedItem(T value, string path, string? error)
    {
        Value = value;
        Path = path;
        Error = error;
    }

    /// <summary>
    /// Gets the bound value, or a default instance when <see cref="Error"/> is set.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Gets the configuration path of the item.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets a safe description of a binding error, or <see langword="null"/> when binding succeeded.
    /// </summary>
    public string? Error { get; }
}

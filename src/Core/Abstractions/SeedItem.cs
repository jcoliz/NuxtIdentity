namespace NuxtIdentity.Core.Abstractions;

/// <summary>
/// A configured seed item bound to a typed options object.
/// </summary>
/// <typeparam name="T">The options type the item is bound to.</typeparam>
public sealed class SeedItem<T>
{
    internal SeedItem(T value, string path, string? error)
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

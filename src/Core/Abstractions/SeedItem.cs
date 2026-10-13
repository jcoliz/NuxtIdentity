namespace NuxtIdentity.Core.Abstractions;

/// <summary>
/// A configured seed item bound to a typed options object.
/// </summary>
/// <typeparam name="T">The options type the item is bound to.</typeparam>
/// <param name="Value">The bound value, or a default instance when <paramref name="Error"/> is set.</param>
/// <param name="Path">The configuration path of the item.</param>
/// <param name="Error">A safe description of a binding error, or <see langword="null"/> when binding succeeded.</param>
public sealed record SeedItem<T>(T Value, string Path, string? Error)
{
    /// <summary>
    /// Returns the item's path and error, omitting <see cref="Value"/> because bound options can contain secrets.
    /// </summary>
    public override string ToString() => $"SeedItem {{ Path = {Path}, Error = {Error} }}";
}

namespace NuxtIdentity.Core.Exceptions;

/// <summary>
/// Base exception for all NuxtIdentity library errors.
/// </summary>
/// <remarks>
/// Consumers can catch this type to handle any NuxtIdentity-specific error
/// in a single catch block, distinguishing library errors from framework or
/// application exceptions.
/// </remarks>
public class NuxtIdentityException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NuxtIdentityException"/> class.
    /// </summary>
    public NuxtIdentityException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NuxtIdentityException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public NuxtIdentityException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NuxtIdentityException"/> class
    /// with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public NuxtIdentityException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

namespace NuxtIdentity.Core.Exceptions;

/// <summary>
/// Thrown when a required service is not registered in the dependency injection container.
/// </summary>
/// <remarks>
/// This exception indicates a configuration error — the consumer forgot to register a
/// required service. For example, the forgot-password endpoint requires an
/// <c>IUserNotifier</c> implementation to deliver reset codes. If none is
/// registered, this exception is thrown so the misconfiguration is immediately visible
/// rather than silently failing.
/// </remarks>
public class NuxtIdentityConfigurationException : NuxtIdentityException
{
    /// <summary>
    /// Gets the name of the service that is missing from the DI container.
    /// </summary>
    public string MissingService { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NuxtIdentityConfigurationException"/> class
    /// with the name of the missing service.
    /// </summary>
    /// <param name="missingService">The name of the required service that is not registered.</param>
    public NuxtIdentityConfigurationException(string missingService)
        : base($"Required service '{missingService}' is not registered in the DI container. " +
               $"Register an implementation of '{missingService}' to use this feature.")
    {
        MissingService = missingService;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NuxtIdentityConfigurationException"/> class
    /// with the name of the missing service and a custom message.
    /// </summary>
    /// <param name="missingService">The name of the required service that is not registered.</param>
    /// <param name="message">A custom error message.</param>
    public NuxtIdentityConfigurationException(string missingService, string message)
        : base(message)
    {
        MissingService = missingService;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NuxtIdentityConfigurationException"/> class
    /// with the name of the missing service, a custom message, and an inner exception.
    /// </summary>
    /// <param name="missingService">The name of the required service that is not registered.</param>
    /// <param name="message">A custom error message.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public NuxtIdentityConfigurationException(string missingService, string message, Exception innerException)
        : base(message, innerException)
    {
        MissingService = missingService;
    }
}

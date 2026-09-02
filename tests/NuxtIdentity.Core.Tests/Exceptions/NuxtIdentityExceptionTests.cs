using NUnit.Framework;
using NuxtIdentity.Core.Exceptions;

namespace NuxtIdentity.Core.Tests.Exceptions;

/// <summary>
/// Tests for <see cref="NuxtIdentityException"/> and <see cref="NuxtIdentityConfigurationException"/>.
/// </summary>
[TestFixture]
public class NuxtIdentityExceptionTests
{
    #region NuxtIdentityException Tests

    [Test]
    public void NuxtIdentityException_DefaultConstructor_CreatesException()
    {
        // Given: No parameters

        // When: Creating a default NuxtIdentityException
        var exception = new NuxtIdentityException();

        // Then: Exception should be created with null message
        Assert.That(exception, Is.AssignableTo<Exception>());
        Assert.That(exception.Message, Is.Not.Null.And.Not.Empty);
        Assert.That(exception.InnerException, Is.Null);
    }

    [Test]
    public void NuxtIdentityException_WithMessage_PreservesMessage()
    {
        // Given: A specific error message
        var message = "Something went wrong in NuxtIdentity";

        // When: Creating the exception with the message
        var exception = new NuxtIdentityException(message);

        // Then: The message should be preserved
        Assert.That(exception.Message, Is.EqualTo(message));
        Assert.That(exception.InnerException, Is.Null);
    }

    [Test]
    public void NuxtIdentityException_WithMessageAndInnerException_PreservesBoth()
    {
        // Given: A message and an inner exception
        var message = "Outer error";
        var inner = new InvalidOperationException("Inner error");

        // When: Creating the exception with both
        var exception = new NuxtIdentityException(message, inner);

        // Then: Both message and inner exception should be preserved
        Assert.That(exception.Message, Is.EqualTo(message));
        Assert.That(exception.InnerException, Is.SameAs(inner));
    }

    [Test]
    public void NuxtIdentityException_IsAssignableToException()
    {
        // Given: A NuxtIdentityException

        // When: Checking the type hierarchy
        var exception = new NuxtIdentityException("test");

        // Then: It should be assignable to Exception
        Assert.That(exception, Is.AssignableTo<Exception>());
    }

    #endregion

    #region NuxtIdentityConfigurationException Tests

    [Test]
    public void ConfigurationException_WithMissingService_SetsProperties()
    {
        // Given: A missing service name
        var missingService = "IUserNotifier";

        // When: Creating the configuration exception
        var exception = new NuxtIdentityConfigurationException(missingService);

        // Then: MissingService property should be set
        Assert.That(exception.MissingService, Is.EqualTo(missingService));

        // And: Message should contain the service name
        Assert.That(exception.Message, Does.Contain(missingService));
    }

    [Test]
    public void ConfigurationException_WithCustomMessage_PreservesMessageAndService()
    {
        // Given: A missing service name and custom message
        var missingService = "IUserNotifier";
        var customMessage = "Please register the notifier.";

        // When: Creating the exception with both
        var exception = new NuxtIdentityConfigurationException(missingService, customMessage);

        // Then: Both should be preserved
        Assert.That(exception.MissingService, Is.EqualTo(missingService));
        Assert.That(exception.Message, Is.EqualTo(customMessage));
    }

    [Test]
    public void ConfigurationException_WithInnerException_PreservesAll()
    {
        // Given: A missing service, custom message, and inner exception
        var missingService = "IUserNotifier";
        var customMessage = "Configuration error";
        var inner = new ArgumentException("bad arg");

        // When: Creating the exception with all parameters
        var exception = new NuxtIdentityConfigurationException(missingService, customMessage, inner);

        // Then: All properties should be preserved
        Assert.That(exception.MissingService, Is.EqualTo(missingService));
        Assert.That(exception.Message, Is.EqualTo(customMessage));
        Assert.That(exception.InnerException, Is.SameAs(inner));
    }

    [Test]
    public void ConfigurationException_InheritsFromNuxtIdentityException()
    {
        // Given: A NuxtIdentityConfigurationException

        // When: Checking the type hierarchy
        var exception = new NuxtIdentityConfigurationException("TestService");

        // Then: It should be assignable to NuxtIdentityException
        Assert.That(exception, Is.AssignableTo<NuxtIdentityException>());

        // And: It should be catchable as NuxtIdentityException
        Assert.That(exception, Is.AssignableTo<Exception>());
    }

    [Test]
    public void ConfigurationException_CanBeCaughtAsNuxtIdentityException()
    {
        // Given: A NuxtIdentityConfigurationException is thrown
        NuxtIdentityException? caught = null;

        // When: Catching it as the base NuxtIdentityException type
        try
        {
            throw new NuxtIdentityConfigurationException("IUserNotifier");
        }
        catch (NuxtIdentityException ex)
        {
            caught = ex;
        }

        // Then: It should be caught and preserve its type
        Assert.That(caught, Is.Not.Null);
        Assert.That(caught, Is.TypeOf<NuxtIdentityConfigurationException>());

        // And: The MissingService property should be accessible after casting
        var configException = (NuxtIdentityConfigurationException)caught!;
        Assert.That(configException.MissingService, Is.EqualTo("IUserNotifier"));
    }

    #endregion
}

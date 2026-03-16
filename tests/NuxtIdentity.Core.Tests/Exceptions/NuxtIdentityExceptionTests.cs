using FluentAssertions;
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
        exception.Should().BeAssignableTo<Exception>();
        exception.Message.Should().NotBeNullOrEmpty();
        exception.InnerException.Should().BeNull();
    }

    [Test]
    public void NuxtIdentityException_WithMessage_PreservesMessage()
    {
        // Given: A specific error message
        var message = "Something went wrong in NuxtIdentity";

        // When: Creating the exception with the message
        var exception = new NuxtIdentityException(message);

        // Then: The message should be preserved
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
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
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(inner);
    }

    [Test]
    public void NuxtIdentityException_IsAssignableToException()
    {
        // Given: A NuxtIdentityException

        // When: Checking the type hierarchy
        var exception = new NuxtIdentityException("test");

        // Then: It should be assignable to Exception
        exception.Should().BeAssignableTo<Exception>();
    }

    #endregion

    #region NuxtIdentityConfigurationException Tests

    [Test]
    public void ConfigurationException_WithMissingService_SetsProperties()
    {
        // Given: A missing service name
        var missingService = "IUserNotifier<TUser>";

        // When: Creating the configuration exception
        var exception = new NuxtIdentityConfigurationException(missingService);

        // Then: MissingService property should be set
        exception.MissingService.Should().Be(missingService);

        // And: Message should contain the service name
        exception.Message.Should().Contain(missingService);
    }

    [Test]
    public void ConfigurationException_WithCustomMessage_PreservesMessageAndService()
    {
        // Given: A missing service name and custom message
        var missingService = "IUserNotifier<TUser>";
        var customMessage = "Please register the notifier.";

        // When: Creating the exception with both
        var exception = new NuxtIdentityConfigurationException(missingService, customMessage);

        // Then: Both should be preserved
        exception.MissingService.Should().Be(missingService);
        exception.Message.Should().Be(customMessage);
    }

    [Test]
    public void ConfigurationException_WithInnerException_PreservesAll()
    {
        // Given: A missing service, custom message, and inner exception
        var missingService = "IUserNotifier<TUser>";
        var customMessage = "Configuration error";
        var inner = new ArgumentException("bad arg");

        // When: Creating the exception with all parameters
        var exception = new NuxtIdentityConfigurationException(missingService, customMessage, inner);

        // Then: All properties should be preserved
        exception.MissingService.Should().Be(missingService);
        exception.Message.Should().Be(customMessage);
        exception.InnerException.Should().BeSameAs(inner);
    }

    [Test]
    public void ConfigurationException_InheritsFromNuxtIdentityException()
    {
        // Given: A NuxtIdentityConfigurationException

        // When: Checking the type hierarchy
        var exception = new NuxtIdentityConfigurationException("TestService");

        // Then: It should be assignable to NuxtIdentityException
        exception.Should().BeAssignableTo<NuxtIdentityException>();

        // And: It should be catchable as NuxtIdentityException
        exception.Should().BeAssignableTo<Exception>();
    }

    [Test]
    public void ConfigurationException_CanBeCaughtAsNuxtIdentityException()
    {
        // Given: A NuxtIdentityConfigurationException is thrown
        NuxtIdentityException? caught = null;

        // When: Catching it as the base NuxtIdentityException type
        try
        {
            throw new NuxtIdentityConfigurationException("IUserNotifier<TUser>");
        }
        catch (NuxtIdentityException ex)
        {
            caught = ex;
        }

        // Then: It should be caught and preserve its type
        caught.Should().NotBeNull();
        caught.Should().BeOfType<NuxtIdentityConfigurationException>();

        // And: The MissingService property should be accessible after casting
        var configException = caught as NuxtIdentityConfigurationException;
        configException!.MissingService.Should().Be("IUserNotifier<TUser>");
    }

    #endregion
}

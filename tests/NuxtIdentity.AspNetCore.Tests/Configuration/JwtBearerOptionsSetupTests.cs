using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using NuxtIdentity.AspNetCore.Configuration;
using NuxtIdentity.AspNetCore.Tests.Helpers;
using NuxtIdentity.Core.Configuration;

namespace NuxtIdentity.AspNetCore.Tests.Configuration;

/// <summary>
/// Tests for JwtBearerOptionsSetup configuration.
/// </summary>
[TestFixture]
[Category("Unit")]
public class JwtBearerOptionsSetupTests
{
    [Test]
    public void Configure_DefaultScheme_ConfiguresTokenValidationParameters()
    {
        // Arrange
        var jwtOptions = TestJwtOptions.Create();
        var optionsWrapper = Options.Create(jwtOptions);
        var setup = new JwtBearerOptionsSetup(optionsWrapper);
        var bearerOptions = new JwtBearerOptions();

        // Act
        setup.Configure(bearerOptions);

        // Assert
        bearerOptions.TokenValidationParameters.Should().NotBeNull();
        bearerOptions.TokenValidationParameters.ValidateIssuerSigningKey.Should().BeTrue();
        bearerOptions.TokenValidationParameters.IssuerSigningKey.Should().NotBeNull();
        bearerOptions.TokenValidationParameters.IssuerSigningKey.Should().BeOfType<SymmetricSecurityKey>();

        var key = bearerOptions.TokenValidationParameters.IssuerSigningKey as SymmetricSecurityKey;
        key!.Key.Should().BeEquivalentTo(jwtOptions.Key);
    }

    [Test]
    public void Configure_DefaultScheme_ValidatesIssuer()
    {
        // Arrange
        var jwtOptions = TestJwtOptions.Create();
        var optionsWrapper = Options.Create(jwtOptions);
        var setup = new JwtBearerOptionsSetup(optionsWrapper);
        var bearerOptions = new JwtBearerOptions();

        // Act
        setup.Configure(bearerOptions);

        // Assert
        bearerOptions.TokenValidationParameters.ValidateIssuer.Should().BeTrue();
        bearerOptions.TokenValidationParameters.ValidIssuer.Should().Be(jwtOptions.Issuer);
    }

    [Test]
    public void Configure_DefaultScheme_ValidatesAudience()
    {
        // Arrange
        var jwtOptions = TestJwtOptions.Create();
        var optionsWrapper = Options.Create(jwtOptions);
        var setup = new JwtBearerOptionsSetup(optionsWrapper);
        var bearerOptions = new JwtBearerOptions();

        // Act
        setup.Configure(bearerOptions);

        // Assert
        bearerOptions.TokenValidationParameters.ValidateAudience.Should().BeTrue();
        bearerOptions.TokenValidationParameters.ValidAudience.Should().Be(jwtOptions.Audience);
    }

    [Test]
    public void Configure_DefaultScheme_ValidatesLifetime()
    {
        // Arrange
        var jwtOptions = TestJwtOptions.Create();
        var optionsWrapper = Options.Create(jwtOptions);
        var setup = new JwtBearerOptionsSetup(optionsWrapper);
        var bearerOptions = new JwtBearerOptions();

        // Act
        setup.Configure(bearerOptions);

        // Assert
        bearerOptions.TokenValidationParameters.ValidateLifetime.Should().BeTrue();
    }

    [Test]
    public void Configure_DefaultScheme_SetsClockSkewToZero()
    {
        // Arrange
        var jwtOptions = TestJwtOptions.Create();
        var optionsWrapper = Options.Create(jwtOptions);
        var setup = new JwtBearerOptionsSetup(optionsWrapper);
        var bearerOptions = new JwtBearerOptions();

        // Act
        setup.Configure(bearerOptions);

        // Assert
        bearerOptions.TokenValidationParameters.ClockSkew.Should().Be(TimeSpan.Zero);
    }

    [Test]
    public void Configure_NamedScheme_ConfiguresOnlyJwtBearerScheme()
    {
        // Arrange
        var jwtOptions = TestJwtOptions.Create();
        var optionsWrapper = Options.Create(jwtOptions);
        var setup = new JwtBearerOptionsSetup(optionsWrapper);
        var bearerOptions = new JwtBearerOptions();

        // Act - Configure with JwtBearer scheme name
        setup.Configure(JwtBearerDefaults.AuthenticationScheme, bearerOptions);

        // Assert
        bearerOptions.TokenValidationParameters.Should().NotBeNull();
        bearerOptions.TokenValidationParameters.ValidateIssuerSigningKey.Should().BeTrue();
    }

    [Test]
    public void Configure_DifferentNamedScheme_DoesNotConfigureCustomParameters()
    {
        // Arrange
        var jwtOptions = TestJwtOptions.Create();
        var optionsWrapper = Options.Create(jwtOptions);
        var setup = new JwtBearerOptionsSetup(optionsWrapper);
        var bearerOptions = new JwtBearerOptions();

        // Act - Configure with different scheme name
        setup.Configure("DifferentScheme", bearerOptions);

        // Assert - Should not configure our custom parameters (IssuerSigningKey should remain null)
        bearerOptions.TokenValidationParameters.Should().NotBeNull("default parameters are created");
        bearerOptions.TokenValidationParameters.IssuerSigningKey.Should().BeNull("we didn't configure it");
        bearerOptions.TokenValidationParameters.ValidateIssuerSigningKey.Should().BeFalse("default is false");
    }

    [Test]
    public void Configure_ParamterlessOverload_CallsNamedOverloadWithDefaultScheme()
    {
        // Arrange
        var jwtOptions = TestJwtOptions.Create();
        var optionsWrapper = Options.Create(jwtOptions);
        var setup = new JwtBearerOptionsSetup(optionsWrapper);
        var bearerOptions = new JwtBearerOptions();

        // Act
        setup.Configure(bearerOptions);

        // Assert - Should be configured (proves it called the named overload)
        bearerOptions.TokenValidationParameters.Should().NotBeNull();
        bearerOptions.TokenValidationParameters.ValidateIssuerSigningKey.Should().BeTrue();
    }
}

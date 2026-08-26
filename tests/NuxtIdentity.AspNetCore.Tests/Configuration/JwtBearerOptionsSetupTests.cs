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
        Assert.That(bearerOptions.TokenValidationParameters, Is.Not.Null);
        Assert.That(bearerOptions.TokenValidationParameters.ValidateIssuerSigningKey, Is.True);
        Assert.That(bearerOptions.TokenValidationParameters.IssuerSigningKey, Is.Not.Null);
        Assert.That(bearerOptions.TokenValidationParameters.IssuerSigningKey, Is.TypeOf<SymmetricSecurityKey>());

        var key = bearerOptions.TokenValidationParameters.IssuerSigningKey as SymmetricSecurityKey;
        Assert.That(key, Is.Not.Null);
        Assert.That(key!.Key, Is.EqualTo(jwtOptions.Key));
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
        Assert.That(bearerOptions.TokenValidationParameters.ValidateIssuer, Is.True);
        Assert.That(bearerOptions.TokenValidationParameters.ValidIssuer, Is.EqualTo(jwtOptions.Issuer));
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
        Assert.That(bearerOptions.TokenValidationParameters.ValidateAudience, Is.True);
        Assert.That(bearerOptions.TokenValidationParameters.ValidAudience, Is.EqualTo(jwtOptions.Audience));
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
        Assert.That(bearerOptions.TokenValidationParameters.ValidateLifetime, Is.True);
    }

    [Test]
    public void Configure_DefaultScheme_SetsClockSkewFromOptions()
    {
        // Arrange
        var jwtOptions = TestJwtOptions.Create();
        var optionsWrapper = Options.Create(jwtOptions);
        var setup = new JwtBearerOptionsSetup(optionsWrapper);
        var bearerOptions = new JwtBearerOptions();

        // Act
        setup.Configure(bearerOptions);

        // Assert
        Assert.That(bearerOptions.TokenValidationParameters.ClockSkew, Is.EqualTo(jwtOptions.ClockSkew));
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
        Assert.That(bearerOptions.TokenValidationParameters, Is.Not.Null);
        Assert.That(bearerOptions.TokenValidationParameters.ValidateIssuerSigningKey, Is.True);
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
        Assert.That(bearerOptions.TokenValidationParameters, Is.Not.Null, "default parameters are created");
        Assert.That(bearerOptions.TokenValidationParameters.IssuerSigningKey, Is.Null, "we didn't configure it");
        Assert.That(bearerOptions.TokenValidationParameters.ValidateIssuerSigningKey, Is.False, "default is false");
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
        Assert.That(bearerOptions.TokenValidationParameters, Is.Not.Null);
        Assert.That(bearerOptions.TokenValidationParameters.ValidateIssuerSigningKey, Is.True);
    }
}

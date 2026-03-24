using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Configuration;
using NuxtIdentity.EntityFrameworkCore.Extensions;
using NuxtIdentity.EntityFrameworkCore.Services;
using NuxtIdentity.EntityFrameworkCore.Tests.Helpers;
using System.Text;

namespace NuxtIdentity.EntityFrameworkCore.Tests.Extensions;

[TestFixture]
[Category("Integration")]
public class ServiceCollectionExtensionsTests
{
    private IServiceCollection _services = null!;
    private IConfiguration _configuration = null!;

    [SetUp]
    public void SetUp()
    {
        _services = new ServiceCollection();

        // Create a minimal configuration with JWT settings
        var configurationData = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("ThisIsATestSecretKeyThatIsLongEnoughForHS256Encryption")),
            ["Jwt:Issuer"] = "TestIssuer",
            ["Jwt:Audience"] = "TestAudience",
            ["Jwt:Lifespan"] = "00:15:00",
            ["Jwt:RefreshTokenLifespan"] = "7.00:00:00"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Add required DbContext for tests
        _services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));

        // Add logging infrastructure (required by EfRefreshTokenService)
        _services.AddLogging();
    }

    [Test]
    public void AddNuxtIdentityEntityFramework_RegistersRefreshTokenService()
    {
        // Given a service collection with DbContext
        _services.AddNuxtIdentityEntityFramework<TestDbContext>();

        // When building the service provider
        var serviceProvider = _services.BuildServiceProvider();

        // Then the IRefreshTokenService should be registered
        var service = serviceProvider.GetService<IRefreshTokenService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<EfRefreshTokenService<TestDbContext>>();
    }

    [Test]
    public void AddNuxtIdentityEntityFramework_RegistersServiceAsScoped()
    {
        // Given a service collection with DbContext
        _services.AddNuxtIdentityEntityFramework<TestDbContext>();

        // When building the service provider
        var serviceProvider = _services.BuildServiceProvider();

        // Then the service should be registered as scoped
        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();

        var service1 = scope1.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var service2 = scope1.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var service3 = scope2.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        // Same scope should return same instance
        service1.Should().BeSameAs(service2);
        // Different scope should return different instance
        service1.Should().NotBeSameAs(service3);
    }

    [Test]
    public void AddNuxtIdentityWithEntityFramework_RegistersAllRequiredServices()
    {
        // Given a service collection with Identity configured with EF stores
        _services.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TestDbContext>()
            .AddDefaultTokenProviders();

        // And NuxtIdentity with EF configured
        _services.AddNuxtIdentityWithEntityFramework<IdentityUser, TestDbContext>(_configuration);

        // When building the service provider
        var serviceProvider = _services.BuildServiceProvider();

        // Then all NuxtIdentity services should be registered
        var refreshTokenService = serviceProvider.GetService<IRefreshTokenService>();
        refreshTokenService.Should().NotBeNull();
        refreshTokenService.Should().BeOfType<EfRefreshTokenService<TestDbContext>>();

        var jwtTokenService = serviceProvider.GetService<IJwtTokenService<IdentityUser>>();
        jwtTokenService.Should().NotBeNull();

        var userClaimsProvider = serviceProvider.GetService<IUserClaimsProvider<IdentityUser>>();
        userClaimsProvider.Should().NotBeNull();
    }

    [Test]
    public void AddNuxtIdentityWithEntityFramework_ConfiguresJwtOptions()
    {
        // Given a service collection with Identity configured with EF stores
        _services.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TestDbContext>()
            .AddDefaultTokenProviders();

        // And NuxtIdentity with EF configured
        _services.AddNuxtIdentityWithEntityFramework<IdentityUser, TestDbContext>(_configuration);

        // When building the service provider
        var serviceProvider = _services.BuildServiceProvider();

        // Then JWT options should be configured from configuration
        var jwtOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtOptions>>();
        jwtOptions.Value.Should().NotBeNull();
        jwtOptions.Value.Key.Should().NotBeNull();
        jwtOptions.Value.Issuer.Should().Be("TestIssuer");
        jwtOptions.Value.Audience.Should().Be("TestAudience");
    }

    [Test]
    public void AddNuxtIdentityWithEntityFramework_ConfiguresAuthentication()
    {
        // Given a service collection with Identity configured with EF stores
        _services.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TestDbContext>()
            .AddDefaultTokenProviders();

        // And NuxtIdentity with EF configured
        _services.AddNuxtIdentityWithEntityFramework<IdentityUser, TestDbContext>(_configuration);

        // When building the service provider
        var serviceProvider = _services.BuildServiceProvider();

        // Then authentication services should be registered
        var authenticationService = serviceProvider.GetService<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
        authenticationService.Should().NotBeNull();
    }

    [Test]
    public void AddNuxtIdentityEntityFramework_WithMultipleContexts_RegistersCorrectImplementation()
    {
        // Given a service collection with a specific DbContext type
        _services.AddNuxtIdentityEntityFramework<TestDbContext>();

        // When building the service provider
        var serviceProvider = _services.BuildServiceProvider();

        // Then the service should use the correct context type
        var service = serviceProvider.GetRequiredService<IRefreshTokenService>();
        service.Should().BeOfType<EfRefreshTokenService<TestDbContext>>();
    }

    [Test]
    public void AddNuxtIdentityEntityFramework_CanResolveServiceInScope()
    {
        // Given a service collection with NuxtIdentity EF configured
        _services.AddNuxtIdentityEntityFramework<TestDbContext>();

        // When building the service provider and creating a scope
        var serviceProvider = _services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        // Then the service should be resolvable within the scope
        var service = scope.ServiceProvider.GetService<IRefreshTokenService>();
        service.Should().NotBeNull();
    }

    [Test]
    public async Task AddNuxtIdentityEntityFramework_ServiceCanPerformOperations()
    {
        // Given a fully configured service collection
        _services.AddNuxtIdentityEntityFramework<TestDbContext>();

        // And JWT options are configured
        _services.Configure<JwtOptions>(options =>
        {
            options.Key = Encoding.UTF8.GetBytes("ThisIsATestSecretKeyThatIsLongEnoughForHS256Encryption");
            options.Issuer = "TestIssuer";
            options.Audience = "TestAudience";
            options.Lifespan = TimeSpan.FromMinutes(15);
            options.RefreshTokenLifespan = TimeSpan.FromDays(7);
        });

        // When building the service provider and getting the service
        var serviceProvider = _services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        // Then the service should be functional
        var token = await service.GenerateRefreshTokenAsync("testUser");
        token.Should().NotBeNullOrEmpty();

        var returnedUserId = await service.ValidateRefreshTokenAsync(token);
        returnedUserId.Should().Be("testUser");
    }

    [Test]
    public void AddNuxtIdentityWithEntityFramework_ReturnsSameServiceCollection()
    {
        // Given a service collection with Identity configured with EF stores
        _services.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TestDbContext>()
            .AddDefaultTokenProviders();

        var originalServices = _services;

        // When calling AddNuxtIdentityWithEntityFramework
        var returnedServices = _services.AddNuxtIdentityWithEntityFramework<IdentityUser, TestDbContext>(_configuration);

        // Then it should return the same service collection for chaining
        returnedServices.Should().BeSameAs(originalServices);
    }

    [Test]
    public void AddNuxtIdentityEntityFramework_ReturnsSameServiceCollection()
    {
        // Given a service collection
        var originalServices = _services;

        // When calling AddNuxtIdentityEntityFramework
        var returnedServices = _services.AddNuxtIdentityEntityFramework<TestDbContext>();

        // Then it should return the same service collection for chaining
        returnedServices.Should().BeSameAs(originalServices);
    }

    [Test]
    public void AddNuxtIdentityEntityFramework_CanBeCalledMultipleTimes()
    {
        // Given a service collection
        // When calling AddNuxtIdentityEntityFramework multiple times
        _services.AddNuxtIdentityEntityFramework<TestDbContext>();
        _services.AddNuxtIdentityEntityFramework<TestDbContext>();

        // Then it should not throw
        Action act = () => _services.BuildServiceProvider();
        act.Should().NotThrow();

        // And the service should still be resolvable
        var serviceProvider = _services.BuildServiceProvider();
        var service = serviceProvider.GetService<IRefreshTokenService>();
        service.Should().NotBeNull();
    }

    [Test]
    public void AddNuxtIdentityEntityFramework_RegistersInvitationService()
    {
        // Given: A service collection with DbContext
        _services.AddNuxtIdentityEntityFramework<TestDbContext>();

        // When: Building the service provider
        var serviceProvider = _services.BuildServiceProvider();

        // Then: The IInvitationService should be registered
        var service = serviceProvider.GetService<IInvitationService>();
        service.Should().NotBeNull();
        service.Should().BeOfType<EfInvitationService<TestDbContext>>();
    }
}

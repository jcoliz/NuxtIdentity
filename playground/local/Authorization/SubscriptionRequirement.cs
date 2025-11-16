using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.Playground.Local.Authorization;

/// <summary>
/// Requirement that the user must have an active subscription.
/// </summary>
/// <remarks>
/// This could be made much more generic
/// </remarks>
public class SubscriptionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The route parameter name that contains the subscription ID.
    /// </summary>
    public string RouteParameterName { get; }

    public SubscriptionRequirement(string routeParameterName = "subscription")
    {
        RouteParameterName = routeParameterName;
    }
}

/// <summary>
/// Handler that validates the user has an active subscription matching the route parameter.
/// </summary>
public class SubscriptionHandler : AuthorizationHandler<SubscriptionRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SubscriptionHandler> _logger;

    public SubscriptionHandler(
        IHttpContextAccessor httpContextAccessor,
        ILogger<SubscriptionHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SubscriptionRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogWarning("No HTTP context available");
            return Task.CompletedTask;
        }

        // Get the subscription ID from the route
        if (!httpContext.Request.RouteValues.TryGetValue(requirement.RouteParameterName, out var subscriptionIdObj) 
            || subscriptionIdObj is not string requiredSubscriptionId)
        {
            _logger.LogWarning("No subscription ID found in route parameter: {ParameterName}", requirement.RouteParameterName);
            return Task.CompletedTask;
        }

        // Get subscription claims from the user
        var subscriptionClaims = context.User.Claims
            .Select(c => SubscriptionClaim.Parse(c))
            .Where(c => c != null)
            .ToList();

        if (!subscriptionClaims.Any())
        {
            _logger.LogWarning("User {UserId} has no subscription claims", context.User.Identity?.Name);
            return Task.CompletedTask;
        }

        var matching = subscriptionClaims.Where(c => c?.SubscriptionId.ToString() == requiredSubscriptionId).ToList();

        if (!matching.Any())
        {
            _logger.LogWarning(
                "User {UserId} has no subscription claim matching ID {SubscriptionId}", 
                context.User.Identity?.Name, 
                requiredSubscriptionId);
            return Task.CompletedTask;
        }

        var valid = matching.Any(c => c?.IsActive == true);
        if (valid)
        {
            _logger.LogInformation(
                "User {UserId} has active subscription {SubscriptionId}", 
                context.User.Identity?.Name, 
                requiredSubscriptionId);
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning(
                "User {UserId} has no active subscription {SubscriptionId}", 
                context.User.Identity?.Name, 
                requiredSubscriptionId);
        }

        return Task.CompletedTask;
    }
}
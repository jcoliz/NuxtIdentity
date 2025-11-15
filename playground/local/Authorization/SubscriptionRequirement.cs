using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.Playground.Local.Authorization;

/// <summary>
/// Requirement that the user must have an active subscription.
/// </summary>
public class SubscriptionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The route parameter name that contains the subscription ID.
    /// </summary>
    public string RouteParameterName { get; }

    public SubscriptionRequirement(string routeParameterName = "subscriptionId")
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
            .Where(c => c.Type == "subscription")
            .ToList();

        if (!subscriptionClaims.Any())
        {
            _logger.LogWarning("User {UserId} has no subscription claims", context.User.Identity?.Name);
            return Task.CompletedTask;
        }

        // Check if any subscription claim matches and is active
        foreach (var claim in subscriptionClaims)
        {
            try
            {
                var subscription = JsonSerializer.Deserialize<SubscriptionInfo>(claim.Value);
                if (subscription != null 
                    && subscription.Id.ToString() == requiredSubscriptionId 
                    && subscription.Status.Contains(SubscriptionStatus.Active))
                {
                    _logger.LogInformation(
                        "User {UserId} has active subscription {SubscriptionId}", 
                        context.User.Identity?.Name, 
                        requiredSubscriptionId);
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize subscription claim: {ClaimValue}", claim.Value);
            }
        }

        _logger.LogWarning(
            "User {UserId} does not have active subscription {SubscriptionId}", 
            context.User.Identity?.Name, 
            requiredSubscriptionId);

        return Task.CompletedTask;
    }
}
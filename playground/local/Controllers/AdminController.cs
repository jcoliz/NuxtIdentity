using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using NuxtIdentity.Playground.Local.Models;
using NuxtIdentity.Playground.Local.Constants;
using System.Security.Claims;
using System.Text.Json;

namespace NuxtIdentity.Playground.Local.Controllers;

/// <summary>
/// Administrative controller for managing users, roles, and subscriptions.
/// </summary>
/// <remarks>
/// All endpoints in this controller require the 'admin' role.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public partial class AdminController(
    UserManager<ApplicationUser> userManager,
    ILogger<AdminController> logger) : ControllerBase
{
    /// <summary>
    /// Changes a user's role.
    /// </summary>
    /// <param name="request">User ID and new role.</param>
    /// <returns>Success if role changed; otherwise, error.</returns>
    [HttpPost("setrole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetRole([FromBody] SetRoleRequest request)
    {
        LogSetRoleAttempt(request.UserId, request.Role);
        
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            LogSetRoleFailed(request.UserId, "User not found");
            return NotFound(new { message = "User not found" });
        }
        
        var validRoles = new[] { "guest", "account", "admin" };
        if (!validRoles.Contains(request.Role))
        {
            LogSetRoleFailed(request.UserId, "Invalid role");
            return BadRequest(new { message = "Invalid role. Must be 'guest', 'account', or 'admin'" });
        }
        
        // Remove all current roles
        var currentRoles = await userManager.GetRolesAsync(user);
        await userManager.RemoveFromRolesAsync(user, currentRoles);
        
        // Add new role
        var result = await userManager.AddToRoleAsync(user, request.Role);
        
        if (!result.Succeeded)
        {
            LogSetRoleFailed(request.UserId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }
        
        LogSetRoleSuccess(request.UserId, request.Role);
        return Ok(new { success = true, userId = request.UserId, role = request.Role });
    }

    /// <summary>
    /// Sets the subscriptions for a user using Identity claims.
    /// </summary>
    /// <param name="request">User ID and subscriptions to set.</param>
    /// <returns>Success if subscriptions updated; otherwise, error.</returns>
    [HttpPost("setsubscriptions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetSubscriptions([FromBody] SetSubscriptionsRequest request)
    {
        LogSetSubscriptionsAttempt(request.UserId, request.Subscriptions.Count);
        
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            LogSetSubscriptionsFailed(request.UserId, "User not found");
            return NotFound(new { message = "User not found" });
        }

        // Remove all existing subscription claims
        var existingClaims = await userManager.GetClaimsAsync(user);
        var subscriptionClaims = existingClaims.Where(c => c.Type == CustomClaimTypes.Subscription).ToList();
        
        if (subscriptionClaims.Any())
        {
            var removeResult = await userManager.RemoveClaimsAsync(user, subscriptionClaims);
            if (!removeResult.Succeeded)
            {
                LogSetSubscriptionsFailed(request.UserId, "Failed to remove existing subscription claims");
                return BadRequest(new { errors = removeResult.Errors.Select(e => e.Description) });
            }
        }

        // Add new subscription claims
        foreach (var subscription in request.Subscriptions)
        {
            var subscriptionJson = JsonSerializer.Serialize(subscription);
            var claim = new Claim(CustomClaimTypes.Subscription, subscriptionJson);
            var addResult = await userManager.AddClaimAsync(user, claim);
            
            if (!addResult.Succeeded)
            {
                LogSetSubscriptionsFailed(request.UserId, $"Failed to add subscription claim for {subscription.Id}");
                return BadRequest(new { errors = addResult.Errors.Select(e => e.Description) });
            }
        }

        LogSetSubscriptionsSuccess(request.UserId, request.Subscriptions.Count);
        return Ok(new { success = true, userId = request.UserId, subscriptionCount = request.Subscriptions.Count });
    }

    /// <summary>
    /// Gets all subscriptions for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>List of subscriptions.</returns>
    [HttpGet("subscriptions/{userId}")]
    [ProducesResponseType(typeof(GetSubscriptionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubscriptions(string userId)
    {
        LogGetSubscriptionsAttempt(userId);
        
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            LogGetSubscriptionsFailed(userId, "User not found");
            return NotFound(new { message = "User not found" });
        }

        var subscriptions = await GetUserSubscriptionsAsync(user);
        
        LogGetSubscriptionsSuccess(userId, subscriptions.Count);
        return Ok(new GetSubscriptionsResponse
        {
            UserId = userId,
            Subscriptions = subscriptions
        });
    }

    /// <summary>
    /// Helper method to retrieve user subscriptions from Identity claims.
    /// </summary>
    private async Task<List<SubscriptionInfo>> GetUserSubscriptionsAsync(ApplicationUser user)
    {
        var claims = await userManager.GetClaimsAsync(user);
        var subscriptionClaims = claims.Where(c => c.Type == CustomClaimTypes.Subscription);
        
        var subscriptions = new List<SubscriptionInfo>();
        foreach (var claim in subscriptionClaims)
        {
            try
            {
                var subscription = JsonSerializer.Deserialize<SubscriptionInfo>(claim.Value);
                if (subscription != null)
                {
                    subscriptions.Add(subscription);
                }
            }
            catch (JsonException ex)
            {
                LogSubscriptionDeserializationError(user.Id, claim.Value, ex.Message);
            }
        }
        
        return subscriptions;
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Set role attempt for user: {userId} to role: {role}")]
    private partial void LogSetRoleAttempt(string userId, string role);

    [LoggerMessage(Level = LogLevel.Information, Message = "Set role successful for user: {userId} to role: {role}")]
    private partial void LogSetRoleSuccess(string userId, string role);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Set role failed for user: {userId}. Reason: {reason}")]
    private partial void LogSetRoleFailed(string userId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Set subscriptions attempt for user: {userId} with {count} subscriptions")]
    private partial void LogSetSubscriptionsAttempt(string userId, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Set subscriptions successful for user: {userId} with {count} subscriptions")]
    private partial void LogSetSubscriptionsSuccess(string userId, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Set subscriptions failed for user: {userId}. Reason: {reason}")]
    private partial void LogSetSubscriptionsFailed(string userId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Get subscriptions attempt for user: {userId}")]
    private partial void LogGetSubscriptionsAttempt(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Get subscriptions successful for user: {userId} with {count} subscriptions")]
    private partial void LogGetSubscriptionsSuccess(string userId, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Get subscriptions failed for user: {userId}. Reason: {reason}")]
    private partial void LogGetSubscriptionsFailed(string userId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize subscription claim for user: {userId}. Value: {claimValue}. Error: {error}")]
    private partial void LogSubscriptionDeserializationError(string userId, string claimValue, string error);

    #endregion
}

/// <summary>
/// Request model for setting a user's role.
/// </summary>
public record SetRoleRequest
{
    /// <summary>
    /// The ID of the user whose role should be changed.
    /// </summary>
    public required string UserId { get; init; }
    
    /// <summary>
    /// The new role to assign ('guest', 'account', or 'admin').
    /// </summary>
    public required string Role { get; init; }
}

/// <summary>
/// Request model for setting user subscriptions.
/// </summary>
public record SetSubscriptionsRequest
{
    /// <summary>
    /// The ID of the user whose subscriptions should be set.
    /// </summary>
    public required string UserId { get; init; }
    
    /// <summary>
    /// The subscriptions to assign to the user.
    /// </summary>
    public required List<SubscriptionInfo> Subscriptions { get; init; }
}

/// <summary>
/// Response model for getting user subscriptions.
/// </summary>
public record GetSubscriptionsResponse
{
    /// <summary>
    /// The user ID.
    /// </summary>
    public required string UserId { get; init; }
    
    /// <summary>
    /// The user's subscriptions.
    /// </summary>
    public required List<SubscriptionInfo> Subscriptions { get; init; }
}

/// <summary>
/// Represents a user subscription.
/// </summary>
public record SubscriptionInfo
{
    /// <summary>
    /// The subscription identifier (e.g., "premium", "enterprise").
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// The current status of the subscription.
    /// </summary>
    public required SubscriptionStatus[] Status { get; init; }
}

/// <summary>
/// Subscription status values.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>
    /// Subscription is currently active.
    /// </summary>
    Active,
    
    /// <summary>
    /// Subscription is inactive.
    /// </summary>
    Inactive
}
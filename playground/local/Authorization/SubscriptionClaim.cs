using System.Security.Claims;
using System.Text.Json;

namespace NuxtIdentity.Playground.Local.Authorization;

/// <summary>
/// Custom claim representing a user's subscription status.
/// </summary>
/// <remarks>
/// Users can activate a 'subscription' to a certain weather source. If they have an active
/// subscription to that source, they can retrieve weather for it. If not, they cannot.
/// </remarks>
public class SubscriptionClaim : Claim
{
    public readonly static string ClaimType = "subscription";

    public SubscriptionClaim(Guid subscription, bool isActive)
        : base(ClaimType, $"{subscription}:{isActive}")
    {
    }

    public static (Guid SubscriptionId, bool IsActive)? Parse(Claim claim)
    {
        if (claim.Type != ClaimType)
            return null;

        var parts = claim.Value.Split(':');
        if (parts.Length != 2)
            return null;

        if (!Guid.TryParse(parts[0], out var subscriptionId))
            return null;

        if (!bool.TryParse(parts[1], out var isActive))
            return null;

        return (subscriptionId, isActive);
    }
}
# Authorization Strategy

This playground demonstrates a two-tiered authorization approach using ASP.NET Core Identity:
1. **Role-Based Access Control (RBAC)** for broad access levels
2. **Subscription Claims** for fine-grained resource access

## Roles

This app has three roles that control general access levels:

### guest
- **Default role** assigned to all newly registered users
- Basic authenticated access
- Cannot access admin endpoints
- Can only access weather sources with active subscriptions

### account
- Standard user with full account privileges
- Same access as guest, but intended for verified/upgraded users
- Can access all features requiring "account" role or lower

### admin
- Full administrative access
- Can manage users, roles, and subscriptions via `/api/admin/*` endpoints
- Can perform all actions available to guest and account users

### How Roles Work

- Roles are stored in ASP.NET Core Identity's `AspNetUserRoles` table
- Each user can have multiple roles, but this app assigns only one at a time
- Roles are added to the JWT token as claims by `IdentityUserClaimsProvider`
- Controllers restrict access using the `Authorize` attribute with specific roles
- When a user's role changes, the new role takes effect on next token refresh

## Subscription Claims

Access to weather sources is controlled by **subscription claims**, which provide fine-grained authorization beyond roles. See [`SubscriptionClaim.cs`](SubscriptionClaim.cs) for implementation details.

### What is a Subscription?

A subscription represents access to a specific resource (in this case, a weather source). Each subscription has:
- **ID**: A GUID identifying the specific resource
- **Status**: Either active (`true`) or inactive (`false`)

### Storage Format

Subscriptions are stored as ASP.NET Core Identity claims with:
- **Claim Type**: `"subscription"`
- **Claim Value**: Colon-delimited format containing the subscription ID and active status
  - Example: `"550e8400-e29b-41d4-a716-446655440000:true"`

A user can have multiple subscription claims for different resources.

### The SubscriptionClaim Helper Class

The `SubscriptionClaim` class provides type-safe creation and parsing of subscription claims. It ensures the claim format is consistent and makes it easy to extract the subscription ID and active status from claim values.

### Authorization Policy

The `SubscriptionRequirement` and `SubscriptionHandler` implement the authorization logic:

1. **Extract** the subscription ID from the route parameter (default: `{subscription}`)
2. **Find** all subscription claims in the user's JWT token
3. **Match** claims with the requested subscription ID
4. **Validate** that at least one matching subscription has `isActive == true`
5. **Authorize** if valid, otherwise return 403 Forbidden

### How It Works in Practice

1. **Admin** assigns subscriptions to a user via `POST /api/admin/setsubscriptions`
2. **Identity** stores each subscription as a claim in the database
3. **Login/Refresh** includes subscription claims in the JWT token
4. **User** requests a weather endpoint with a subscription ID in the route
5. **Handler** checks if the user has an active subscription for that GUID
6. **Response**: 200 OK if authorized, 403 Forbidden if not

### Why Subscriptions Instead of Just Roles?

Roles answer: "What type of user is this?"  
Subscriptions answer: "Which specific resources can this user access?"

**Benefits:**
- **Scalable**: Add/remove access to individual resources without creating new roles
- **Flexible**: Same user can have different subscriptions with different statuses
- **Dynamic**: Easy to enable/disable access to specific resources
- **Auditable**: Each subscription is a discrete claim with clear meaning

**Use Cases:**
- Multi-tenant SaaS (different customers have access to different data sources)
- Feature flags (enable/disable features per user)
- Time-limited access (mark subscription inactive when expired)
- Trial vs. paid features
- A/B testing cohorts

## Combining Roles and Subscriptions

You can use both authorization mechanisms together. For example, a protected endpoint can require both a minimum role level (like "account") AND an active subscription to a specific resource. This two-tiered approach provides both broad access control (roles) and fine-grained resource access (subscriptions).
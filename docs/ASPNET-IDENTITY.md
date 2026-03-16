# Mapping to ASP.NET Core Identity

NuxtIdentity surfaces key ASP.NET Core Identity details through to the Nuxt front-end in two different forms.

* User object: Returned by `/login` and `/session` endpoints
* Access token: Embedded into the JWT returned by `/login` and `/refresh` endpoints.

By default, the user object takes this form:

```json
{
    "id": "guid",
    "email": "user@domain.com",
    "userName": "User Name",
    "roles": [
        "role_name",
        "second_role_name"
    ],
    "claims": [
        {
            "type": "claim_type",
            "value": "claim_value"
        },
        {
            "type": "second_claim_type",
            "value": "second_claim_value"
        }
    ]
}
```

If you use a derived class for identity, *e.g.* some form of `ApplicationUser`, you'll need to implement the controller methods for at least the `/session` endpoint to return your specific properties.

## User Identity

The [IdentityUser](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.entityframeworkcore.identityuser?view=aspnetcore-1.1&viewFallbackFrom=aspnetcore-10.0) class is mapped as follows

| IdentityUser | User Object | Token Claims |
| -- | -- | -- |
| Id | id (string) | ... |
| Email | email | ... |
| UserName | userName | ... |

## User Roles

The composition, meaning, ordering, and policies of roles are left to the app itself. If any roles are
stored on a user, they will be returned:

* User object: In the `roles` array
* Access token: Each role in a claim of type `...`

# User/Role Claims

Again, the meaning of user or role claims is left to the app. If any claims are stored on a user, they
will be returned. Likewise, any claims associated with any roles stored on the user will be be added to
the list.

## Password Management

NuxtIdentity provides three password management endpoints that wrap ASP.NET Core Identity's existing password reset infrastructure:

| NuxtIdentity Endpoint | ASP.NET Core Identity Method | Purpose |
|---|---|---|
| `POST /api/auth/forgot-password` | `UserManager.GeneratePasswordResetTokenAsync()` | Generates a password reset token |
| `POST /api/auth/reset-password` | `UserManager.ResetPasswordAsync()` | Validates the token and sets a new password |
| `POST /api/auth/change-password` | `UserManager.ChangePasswordAsync()` | Changes password for an authenticated user |

### How Password Reset Tokens Work

ASP.NET Core Identity uses the [Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction) token provider to generate password reset tokens. These tokens are **self-contained** — no additional database storage is required. The token encodes the user's security stamp, so it automatically becomes invalid when the password is changed.

NuxtIdentity does not store, email, or deliver the reset token. Instead, the `IUserNotifier<TUser>` abstraction receives the raw token, and **the consumer is responsible for delivering it** to the user however they choose (email, SMS, push notification, etc.).

### Security Considerations

- **User enumeration prevention**: The `forgot-password` endpoint always returns `200 OK` regardless of whether the user exists
- **Token revocation**: Both `reset-password` and `change-password` revoke all existing refresh tokens for the user after a successful password change
- **Password validation**: ASP.NET Core Identity's configured password validators are applied to the new password in both reset and change flows

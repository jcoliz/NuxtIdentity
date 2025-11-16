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

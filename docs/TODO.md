# V1 Scope

- JWT token generation from ASP.NET Core Identity
- Compatible with nuxt-auth's `local` provider
- Provide all endpoints needed by `local` provider
- `/api/auth/login`, `/api/auth/logout`, `/api/auth/refresh`, `/api/auth/signup` endpoints

## Later

- Compatible with nuxt-auth's `authjs` provider
- User registration/management helpers

### 11. **Documentation**

**Essential docs:**
- README with quick start
- Sample projects
- API documentation (XML comments)
- Migration guide from playground to library
- Security best practices

## 12. **Package Structure**

```
NuxtIdentity/
├── src/
│   ├── NuxtIdentity.Core/
│   ├── NuxtIdentity.EntityFrameworkCore/
│   ├── NuxtIdentity.AspNetIdentity/
│   └── NuxtIdentity.Testing/
├── samples/
│   ├── BasicAuth/
│   ├── WithIdentity/
│   └── CustomImplementation/
└── tests/
    ├── NuxtIdentity.Core.Tests/
    └── NuxtIdentity.Integration.Tests/
```

## 13. **Security Considerations**

**Don't ship with defaults that are insecure:**
- No default JWT keys
- Require explicit configuration
- Validate configuration on startup
- Document security best practices

## Additional Features

The core goal of the library is to connect a Nuxt frontend with ASP.NET Core Identity. However, it's worth considering more advanced functionality as well...

### 1. **Password Reset/Change Flow**
- Request password reset (send reset token)
- Verify reset token
- Reset password with token
- Change password (for authenticated users)

### 2. **Email Verification**
- Send verification email
- Verify email with token
- Resend verification email

### 3. **Account Management**
- Update user profile (name, email)
- Delete account

### 4. **Remember Me / Persistent Sessions**
Different session durations based on "remember me" checkbox

### 5. **Rate Limiting / Brute Force Protection**
- Limit login attempts
- Account lockout after failed attempts
- CAPTCHA integration

### 6. **Multi-Factor Authentication (MFA)**
- TOTP (Time-based One-Time Password)
- SMS codes
- Backup codes

### 7. **Session Management**
- List active sessions
- Revoke specific sessions
- Revoke all other sessions

### 8. **OAuth/Social Login Integration**
While you have local auth, many apps also support:
- Google, GitHub, Microsoft, etc.


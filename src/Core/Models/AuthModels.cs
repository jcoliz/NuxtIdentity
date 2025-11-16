namespace NuxtIdentity.Core.Models;

#region Request Models

public record LoginRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public record SignUpRequest
{
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public record RefreshRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

#endregion

#region Response Models

public record LoginResponse
{
    public TokenPair Token { get; init; } = new();
    public UserInfo User { get; init; } = new();
}

public record RefreshResponse
{
    public TokenPair Token { get; init; } = new();
}

public record SessionResponse
{
    public UserInfo? User { get; init; } = new();
}

#endregion

#region Data Models

public record TokenPair
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
}

public record UserInfo
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string[] Roles { get; init; } = [];
    public ClaimInfo[] Claims { get; init; } = [];
}

public record ClaimInfo
{
    public string Type { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

#endregion

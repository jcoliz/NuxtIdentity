namespace NuxtIdentity.Core.Tests.Helpers;

/// <summary>
/// Simple test user class for testing generic JWT token service.
/// </summary>
public class TestUser
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

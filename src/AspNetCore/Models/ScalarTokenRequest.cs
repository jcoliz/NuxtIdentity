using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace NuxtIdentity.AspNetCore.Models;

/// <summary>
/// Request model for exchanging a scalar token.
/// </summary>
/// <remarks>
/// This is used for implementing Scalar Token Exchange.
/// 
/// NOTE: These are in AspNetCore project due to their use of the FromForm binding.
/// </remarks>
public record ScalarTokenRequest
{
    /// <summary>The OAuth grant type submitted as <c>grant_type</c>.</summary>
    [FromForm(Name = "grant_type")]
    public string? GrantType { get; init; }

    /// <summary>The username submitted for password-based grant requests.</summary>
    [FromForm]
    public string? Username { get; init; }

    /// <summary>The password submitted for password-based grant requests.</summary>
    [FromForm]
    public string? Password { get; init; }

    /// <summary>The refresh token submitted as <c>refresh_token</c>.</summary>
    [FromForm(Name = "refresh_token")]
    public string? RefreshToken { get; init; }
}

/// <summary>An OAuth-style access token response for the API reference.</summary>
/// <param name="AccessToken">JWT used to authorize API requests.</param>
/// <param name="TokenType">Authorization header scheme.</param>
/// <param name="RefreshToken">Refresh token replaced on every successful refresh.</param>
public sealed record ScalarTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("refresh_token")] string RefreshToken);

/// <summary>An OAuth-style token request error.</summary>
/// <param name="Error">OAuth error code.</param>
/// <param name="ErrorDescription">Non-sensitive explanation of the error.</param>
public sealed record ScalarTokenError(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("error_description")] string ErrorDescription);

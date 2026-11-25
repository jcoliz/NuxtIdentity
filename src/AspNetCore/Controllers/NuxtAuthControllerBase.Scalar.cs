
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NuxtIdentity.AspNetCore.Models;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.AspNetCore.Controllers;

public abstract partial class NuxtAuthControllerBase<TUser>
{
    #region Scalar Token Exchange

    /// <summary>
    /// Exchanges form-encoded credentials or a refresh token for tokens in Scalar.
    /// </summary>
    /// <remarks>
    /// This helper should only be used in development and highly trusted environments.
    /// </remarks>
    /// <param name="request">The scalar token request containing credentials or refresh token.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>An <see cref="IActionResult"/> representing the outcome of the token exchange.</returns>
    public async Task<IActionResult> ExchangeScalarToken(ScalarTokenRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.GrantType))
        {
            LogTokenRequestRejected("invalid_request");
            return BadRequest(new ScalarTokenError("invalid_request", "grant_type is required."));
        }

        if (request.GrantType is not ("password" or "refresh_token"))
        {
            LogTokenRequestRejected("unsupported_grant_type");
            return BadRequest(new ScalarTokenError("unsupported_grant_type", "Only password and refresh_token grants are supported."));
        }

        IActionResult result;
        if (request.GrantType == "refresh_token")
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                LogTokenRequestRejected("invalid_request");
                return BadRequest(new ScalarTokenError("invalid_request", "refresh_token is required."));
            }

            result = await RefreshTokens(new RefreshRequest { RefreshToken = request.RefreshToken });
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                LogTokenRequestRejected("invalid_request");
                return BadRequest(new ScalarTokenError("invalid_request", "username and password are required."));
            }

            result = await Login(new LoginRequest { Username = request.Username, Password = request.Password });
        }

        var tokens = result switch
        {
            OkObjectResult { Value: LoginResponse login } => login.Token,
            OkObjectResult { Value: RefreshResponse refresh } => refresh.Token,
            _ => null
        };
        if (tokens is not null)
        {
            LogTokenOk();
            return Ok(new ScalarTokenResponse(tokens.AccessToken, "Bearer", tokens.RefreshToken));
        }

        if (result is ObjectResult { StatusCode: StatusCodes.Status401Unauthorized })
        {
            // RFC 6749 §5.2 specifies 400 Bad Request with error: "invalid_grant" when:
            // - The user’s username/password is invalid.
            // - A refresh token is invalid, expired, revoked, or already used.
            return BadRequest(new ScalarTokenError("invalid_grant", "Invalid credentials or refresh token."));
        }

        return result;
    }

    #endregion
}
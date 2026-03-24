namespace NuxtIdentity.Core.Models;

/// <summary>
/// Controls how user registration behaves on the <c>SignUp</c> endpoint.
/// </summary>
/// <remarks>
/// The controller exposes a virtual <c>RegistrationOptions</c> property that developers override
/// to change registration behavior. The default for Phase 1 is <see cref="Open"/> to avoid
/// the <see cref="EmailConfirmation"/> trap (which is not yet implemented).
/// </remarks>
public enum RegistrationMode
{
    /// <summary>
    /// Anyone can register, no email confirmation required.
    /// </summary>
    Open,

    /// <summary>
    /// Anyone can register but must confirm their email address.
    /// </summary>
    /// <remarks>
    /// Phase 3 — not implemented yet. Setting this mode in Phase 1 will cause the
    /// <c>SignUp</c> endpoint to throw <see cref="System.NotImplementedException"/>.
    /// </remarks>
    EmailConfirmation,

    /// <summary>
    /// An invitation code is required to register. Email is auto-confirmed on acceptance.
    /// </summary>
    InvitationOnly
}

/// <summary>
/// Configuration options for user registration behavior.
/// </summary>
/// <param name="Mode">The registration mode controlling how users can sign up.</param>
public record RegistrationOptions(RegistrationMode Mode = RegistrationMode.Open);

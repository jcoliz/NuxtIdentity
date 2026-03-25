using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.EntityFrameworkCore.Services;

/// <summary>
/// Entity Framework Core implementation of <see cref="IInvitationService"/>.
/// </summary>
/// <typeparam name="TContext">The DbContext type that contains Invitations DbSet.</typeparam>
/// <remarks>
/// This implementation stores invitations in a database using Entity Framework Core.
/// Invitation codes are stored as GUIDs directly (no hashing, unlike refresh tokens)
/// because they need to be looked up by exact match and returned to the admin who created them.
///
/// The DbContext must have a DbSet&lt;InvitationEntity&gt; configured. You can add this
/// to your context like:
/// <code>
/// public DbSet&lt;InvitationEntity&gt; Invitations =&gt; Set&lt;InvitationEntity&gt;();
/// </code>
/// </remarks>
public partial class EfInvitationService<TContext> : IInvitationService
    where TContext : DbContext
{
    private readonly TContext _context;
    private readonly ILogger<EfInvitationService<TContext>> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfInvitationService{TContext}"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="timeProvider">Time provider for testable time operations. Defaults to system time if not provided.</param>
    public EfInvitationService(
        TContext context,
        ILogger<EfInvitationService<TContext>> logger,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Default expiration duration used when <c>expiresIn</c> is not specified.
    /// </summary>
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromDays(30);

    /// <inheritdoc/>
    public async Task<InvitationEntity> CreateAsync(string? email = null,
        IReadOnlyList<string>? roles = null, IReadOnlyList<ClaimInfo>? claims = null,
        TimeSpan? expiresIn = null, string? metadata = null)
    {
        LogStarting();

        var effectiveRoles = roles ?? [];
        var effectiveClaims = claims ?? [];
        var effectiveExpiresIn = expiresIn ?? DefaultExpiration;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var entity = new InvitationEntity
        {
            Code = Guid.NewGuid(),
            Email = email,
            Status = InvitationStatus.Pending,
            Roles = effectiveRoles.Count > 0 ? JsonSerializer.Serialize(effectiveRoles) : null,
            Claims = effectiveClaims.Count > 0 ? JsonSerializer.Serialize(effectiveClaims) : null,
            Metadata = metadata,
            CreatedAt = now,
            ExpiresAt = now.Add(effectiveExpiresIn)
        };

        _context.Set<InvitationEntity>().Add(entity);
        await _context.SaveChangesAsync();

        LogOkInvitationId(entity.Id);
        return entity;
    }

    /// <inheritdoc/>
    public async Task<InvitationEntity> CreateTestAsync(InvitationEntity invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        LogStarting();

        if (string.IsNullOrEmpty(invitation.Email))
        {
            throw new ArgumentException(
                "Email is required for test invitations.", nameof(invitation));
        }

        // Force test flag — caller cannot override
        invitation.IsTest = true;

        // Default status to Pending if not explicitly set (NotFound should never be stored)
        if (invitation.Status == InvitationStatus.NotFound)
        {
            invitation.Status = InvitationStatus.Pending;
        }

        // Auto-generate code if not provided
        if (invitation.Code == Guid.Empty)
        {
            invitation.Code = Guid.NewGuid();
        }

        // Default timestamps if not provided
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (invitation.CreatedAt == default)
        {
            invitation.CreatedAt = now;
        }
        if (invitation.ExpiresAt == default)
        {
            invitation.ExpiresAt = now.Add(DefaultExpiration);
        }

        // Reset Id so EF auto-generates it
        invitation.Id = 0;

        _context.Set<InvitationEntity>().Add(invitation);
        await _context.SaveChangesAsync();

        LogOkInvitationId(invitation.Id);
        return invitation;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteTestInvitationsAsync()
    {
        LogStarting();

        var testInvitations = await _context.Set<InvitationEntity>()
            .Where(e => e.IsTest)
            .ToListAsync();

        var count = testInvitations.Count;
        if (count > 0)
        {
            _context.Set<InvitationEntity>().RemoveRange(testInvitations);
            await _context.SaveChangesAsync();
        }

        LogDeletedTestInvitations(count);
        return count;
    }

    /// <inheritdoc/>
    public async Task<InvitationEntity?> GetByCodeAsync(string code)
    {
        LogStarting();

        if (!Guid.TryParse(code, out var guid))
        {
            LogInvalidCodeFormat();
            return null;
        }

        var entity = await _context.Set<InvitationEntity>()
            .FirstOrDefaultAsync(e => e.Code == guid);

        if (entity == null)
        {
            LogCodeNotFound();
        }
        else
        {
            LogOkInvitationId(entity.Id);
        }

        return entity;
    }

    /// <inheritdoc/>
    public async Task<InvitationStatus> ResolveStatusAsync(string code)
    {
        LogStarting();

        var entity = await GetByCodeAsync(code);
        if (entity == null)
        {
            return InvitationStatus.NotFound;
        }

        var effectiveStatus = GetEffectiveStatus(entity);
        LogOkInvitationIdStatus(entity.Id, effectiveStatus);
        return effectiveStatus;
    }

    /// <inheritdoc/>
    public async Task<InvitationEntity?> ValidateAsync(string code)
    {
        LogStarting();

        var entity = await GetByCodeAsync(code);
        if (entity == null)
        {
            return null;
        }

        var effectiveStatus = GetEffectiveStatus(entity);
        if (effectiveStatus != InvitationStatus.Pending)
        {
            LogInvitationNotUsable(entity.Id, effectiveStatus);
            return null;
        }

        LogOkInvitationId(entity.Id);
        return entity;
    }

    /// <inheritdoc/>
    public async Task AcceptAsync(InvitationEntity invitation, string userId)
    {
        LogStartingInvitationId(invitation.Id);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        invitation.Status = InvitationStatus.Accepted;
        invitation.AcceptedAt = now;
        invitation.AcceptedByUserId = userId;

        await _context.SaveChangesAsync();

        LogOkInvitationIdAccepted(invitation.Id, userId);
    }

    /// <summary>
    /// Computes the effective status of an invitation, accounting for time-based expiration.
    /// </summary>
    /// <param name="entity">The invitation entity to evaluate.</param>
    /// <returns>The effective status: terminal states return as-is; Pending invitations past ExpiresAt return Expired.</returns>
    private InvitationStatus GetEffectiveStatus(InvitationEntity entity)
    {
        // Terminal states return as-is
        if (entity.Status is InvitationStatus.Accepted or InvitationStatus.Revoked)
        {
            return entity.Status;
        }

        // Pending invitations past ExpiresAt are effectively expired
        if (entity.Status == InvitationStatus.Pending &&
            entity.ExpiresAt < _timeProvider.GetUtcNow().UtcDateTime)
        {
            return InvitationStatus.Expired;
        }

        return entity.Status;
    }

    #region Logger Messages

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Debug, "{Location}: Starting invitation {InvitationId}")]
    private partial void LogStartingInvitationId(int invitationId, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Information, "{Location}: OK invitation {InvitationId}")]
    private partial void LogOkInvitationId(int invitationId, [CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Information, "{Location}: OK invitation {InvitationId} status {Status}")]
    private partial void LogOkInvitationIdStatus(int invitationId, InvitationStatus status, [CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Information, "{Location}: OK invitation {InvitationId} accepted by {UserId}")]
    private partial void LogOkInvitationIdAccepted(int invitationId, string userId, [CallerMemberName] string? location = null);

    [LoggerMessage(6, LogLevel.Debug, "{Location}: Invalid code format")]
    private partial void LogInvalidCodeFormat([CallerMemberName] string? location = null);

    [LoggerMessage(7, LogLevel.Debug, "{Location}: Code not found")]
    private partial void LogCodeNotFound([CallerMemberName] string? location = null);

    [LoggerMessage(8, LogLevel.Debug, "{Location}: Invitation {InvitationId} not usable, status {Status}")]
    private partial void LogInvitationNotUsable(int invitationId, InvitationStatus status, [CallerMemberName] string? location = null);

    [LoggerMessage(9, LogLevel.Information, "{Location}: Deleted {Count} test invitations")]
    private partial void LogDeletedTestInvitations(int count, [CallerMemberName] string? location = null);

    #endregion
}

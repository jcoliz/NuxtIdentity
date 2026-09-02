using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.AspNetCore.Services;

/// <summary>
/// In-memory implementation of <see cref="IUserNotifier"/> that captures notifications for testing.
/// </summary>
/// <remarks>
/// This implementation stores all notification data in memory, including the user's identity
/// (UserId and UserName from <see cref="NotificationRecipient"/>), allowing test code
/// to retrieve reset codes and confirmation codes along with the affected user.
///
/// Register this as a singleton in test environments so all requests share the same notification store.
///
/// This is necessary because ASP.NET Identity's codes are generated via data protection token
/// providers — the plaintext code is passed to <see cref="IUserNotifier"/> once and only
/// a hash is stored internally. The code cannot be retrieved after the fact, and regenerating
/// it invalidates the previous one.
/// </remarks>
public class InMemoryUserNotifier : IUserNotifier
{
    private readonly List<NotificationRecord> _notifications = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryUserNotifier"/> class.
    /// </summary>
    /// <param name="timeProvider">Time provider for testable time operations. Defaults to system time if not provided.</param>
    public InMemoryUserNotifier(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async Task SendResetCodeAsync(NotificationRecipient recipient, string resetCode)
    {
        await _lock.WaitAsync();
        try
        {
            _notifications.Add(new NotificationRecord
            {
                UserId = recipient.UserId,
                UserName = recipient.UserName,
                Code = resetCode,
                Type = NotificationType.PasswordReset,
                Timestamp = _timeProvider.GetUtcNow().DateTime
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task SendEmailConfirmationAsync(NotificationRecipient recipient, string confirmationCode)
    {
        await _lock.WaitAsync();
        try
        {
            _notifications.Add(new NotificationRecord
            {
                UserId = recipient.UserId,
                UserName = recipient.UserName,
                Code = confirmationCode,
                Type = NotificationType.EmailConfirmation,
                Timestamp = _timeProvider.GetUtcNow().DateTime
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets all captured notifications.
    /// </summary>
    /// <returns>A read-only list of all notifications captured since creation or the last clear.</returns>
    public async Task<IReadOnlyList<NotificationRecord>> GetNotificationsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return _notifications.ToList().AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Clears all captured notifications.
    /// </summary>
    public async Task ClearAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _notifications.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }
}

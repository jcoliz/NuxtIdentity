using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuxtIdentity.AspNetCore.Configuration;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.Core.Models;

namespace NuxtIdentity.AspNetCore.Services;

/// <summary>
/// Applies configured Identity and invitation seed data without modifying existing records.
/// </summary>
/// <typeparam name="TUser">The application's Identity user type.</typeparam>
public sealed partial class IdentitySeeder<TUser>
    where TUser : IdentityUser
{
    private const string SectionName = "IdentitySeeder";

    private readonly IConfiguration _configuration;
    private readonly UserManager<TUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IInvitationService? _invitationService;
    private readonly ILogger<IdentitySeeder<TUser>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentitySeeder{TUser}"/> class.
    /// </summary>
    public IdentitySeeder(
        IConfiguration configuration,
        UserManager<TUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IServiceProvider serviceProvider,
        ILogger<IdentitySeeder<TUser>> logger)
    {
        _configuration = configuration;
        _userManager = userManager;
        _roleManager = roleManager;
        _invitationService = serviceProvider.GetService<IInvitationService>();
        _logger = logger;
    }

    /// <summary>
    /// Seeds configured groups in deterministic order.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var summary = new SeedSummary();
        var groups = ReadGroups(_configuration.GetSection(SectionName))
            .OrderBy(group => group.Options.Order)
            .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (_invitationService is null)
        {
            var invitationCount = groups.Sum(group => group.Options.Invitations.Count);
            if (invitationCount > 0)
            {
                LogInvitationServiceNotRegistered(invitationCount);
                summary.Skipped += invitationCount;
            }
        }

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (group.Options.ConfigurationError is not null)
            {
                RecordConfigurationFailure(summary, group.Name, group.Path, "group",
                    group.Options.ConfigurationError);
            }

            await SeedRolesAsync(group, summary, cancellationToken);
            await SeedUsersAsync(group, summary, cancellationToken);
            await SeedUserRolesAsync(group, summary, cancellationToken);
            await SeedUserClaimsAsync(group, summary, cancellationToken);
            await SeedRoleClaimsAsync(group, summary, cancellationToken);
            if (_invitationService is { } invitationService)
                await SeedInvitationsAsync(group, invitationService, summary, cancellationToken);
        }

        if (summary.Failed == 0 && summary.Skipped == 0)
        {
            LogSeedingCompleted(summary.Succeeded, summary.Failed, summary.Skipped);
        }
        else
        {
            LogSeedingPartiallyCompleted(summary.Succeeded, summary.Failed, summary.Skipped);
        }
    }

    private async Task SeedRolesAsync(
        SeedGroup group,
        SeedSummary summary,
        CancellationToken cancellationToken)
    {
        foreach (var options in group.Options.Roles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = options.ConfigurationPath ?? group.Path;
            await RunOperationAsync(group.Name, path, "role", SafeKey(options.Name), summary, cancellationToken, async () =>
            {
                if (options.ConfigurationError is not null)
                    return SeedOperationResult.Failure(options.ConfigurationError);

                if (string.IsNullOrWhiteSpace(options.Name))
                    return SeedOperationResult.Failure("Role name is required.");

                var existingRole = await _roleManager.FindByNameAsync(options.Name);
                if (existingRole is not null)
                    return SeedOperationResult.Success();

                string? id = null;
                if (!string.IsNullOrWhiteSpace(options.Id))
                {
                    if (!Guid.TryParse(options.Id, out var roleId))
                        return SeedOperationResult.Failure("Role ID must be a valid GUID.");

                    id = roleId.ToString("D");
                }

                var role = new IdentityRole(options.Name);
                if (id is not null)
                    role.Id = id;

                return IdentityResultToOutcome(await _roleManager.CreateAsync(role));
            });
        }
    }

    private async Task SeedUsersAsync(
        SeedGroup group,
        SeedSummary summary,
        CancellationToken cancellationToken)
    {
        foreach (var options in group.Options.Users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = options.ConfigurationPath ?? group.Path;
            await RunOperationAsync(group.Name, path, "user", SafeKey(options.UserName), summary, cancellationToken, async () =>
            {
                if (options.ConfigurationError is not null)
                    return SeedOperationResult.Failure(options.ConfigurationError);

                if (string.IsNullOrWhiteSpace(options.UserName))
                    return SeedOperationResult.Failure("Username is required.");

                if (await _userManager.FindByNameAsync(options.UserName) is not null)
                    return SeedOperationResult.Success();

                var user = Activator.CreateInstance<TUser>() ??
                    throw new InvalidOperationException("The configured Identity user type could not be instantiated.");
                user.UserName = options.UserName;
                user.Email = options.Email;
                user.EmailConfirmed = options.EmailConfirmed;

                var result = string.IsNullOrEmpty(options.Password)
                    ? await _userManager.CreateAsync(user)
                    : await _userManager.CreateAsync(user, options.Password);

                return IdentityResultToOutcome(result);
            });
        }
    }

    private async Task SeedUserRolesAsync(
        SeedGroup group,
        SeedSummary summary,
        CancellationToken cancellationToken)
    {
        foreach (var userOptions in group.Options.Users)
        {
            for (var roleIndex = 0; roleIndex < userOptions.Roles.Count; roleIndex++)
            {
                var roleName = userOptions.Roles[roleIndex];
                cancellationToken.ThrowIfCancellationRequested();
                var key = $"{SafeKey(userOptions.UserName)} -> {SafeKey(roleName)}";
                var path = roleIndex < userOptions.RoleConfigurationPaths.Count
                    ? userOptions.RoleConfigurationPaths[roleIndex]
                    : userOptions.ConfigurationPath is null
                        ? group.Path
                        : $"{userOptions.ConfigurationPath}:Roles:{roleIndex}";

                await RunOperationAsync(group.Name, path, "user role", key, summary, cancellationToken, async () =>
                {
                    if (string.IsNullOrWhiteSpace(userOptions.UserName))
                        return SeedOperationResult.Failure("Username is required for role assignment.");

                    if (string.IsNullOrWhiteSpace(roleName))
                        return SeedOperationResult.Failure("Role name is required for role assignment.");

                    var user = await _userManager.FindByNameAsync(userOptions.UserName);
                    var role = await _roleManager.FindByNameAsync(roleName);
                    if (user is null || role is null)
                        return SeedOperationResult.Skipped("Required user or role is unavailable.");

                    var currentRoles = await _userManager.GetRolesAsync(user);
                    if (currentRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase))
                        return SeedOperationResult.Success();

                    return IdentityResultToOutcome(await _userManager.AddToRoleAsync(user, roleName));
                });
            }
        }
    }

    private async Task SeedUserClaimsAsync(
        SeedGroup group,
        SeedSummary summary,
        CancellationToken cancellationToken)
    {
        foreach (var userOptions in group.Options.Users)
        {
            foreach (var claimOptions in userOptions.Claims)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = claimOptions.ConfigurationPath ?? userOptions.ConfigurationPath ?? group.Path;
                await RunOperationAsync(group.Name, path, "user claim", SafeKey(userOptions.UserName), summary, cancellationToken, async () =>
                {
                    if (claimOptions.ConfigurationError is not null)
                        return SeedOperationResult.Failure(claimOptions.ConfigurationError);
                    if (string.IsNullOrWhiteSpace(userOptions.UserName))
                        return SeedOperationResult.Failure("Username is required for user claim.");
                    if (!TryGetClaim(claimOptions, out var claim, out var error))
                        return SeedOperationResult.Failure(error);

                    var user = await _userManager.FindByNameAsync(userOptions.UserName);
                    if (user is null)
                        return SeedOperationResult.Skipped("Required user is unavailable.");

                    var existingClaims = await _userManager.GetClaimsAsync(user);
                    if (existingClaims.Any(existing =>
                            existing.Type == claim.Type && existing.Value == claim.Value))
                        return SeedOperationResult.Success();

                    return IdentityResultToOutcome(await _userManager.AddClaimAsync(user, claim));
                });
            }
        }
    }

    private async Task SeedRoleClaimsAsync(
        SeedGroup group,
        SeedSummary summary,
        CancellationToken cancellationToken)
    {
        foreach (var roleClaimsOptions in group.Options.RoleClaims)
        {
            foreach (var claimOptions in roleClaimsOptions.Claims)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = claimOptions.ConfigurationPath ?? roleClaimsOptions.ConfigurationPath ?? group.Path;
                await RunOperationAsync(group.Name, path, "role claim", SafeKey(roleClaimsOptions.Role), summary, cancellationToken, async () =>
                {
                    if (roleClaimsOptions.ConfigurationError is not null)
                        return SeedOperationResult.Failure(roleClaimsOptions.ConfigurationError);
                    if (claimOptions.ConfigurationError is not null)
                        return SeedOperationResult.Failure(claimOptions.ConfigurationError);
                    if (string.IsNullOrWhiteSpace(roleClaimsOptions.Role))
                        return SeedOperationResult.Failure("Role name is required for role claim.");
                    if (!TryGetClaim(claimOptions, out var claim, out var error))
                        return SeedOperationResult.Failure(error);

                    var role = await _roleManager.FindByNameAsync(roleClaimsOptions.Role);
                    if (role is null)
                        return SeedOperationResult.Skipped("Required role is unavailable.");

                    var existingClaims = await _roleManager.GetClaimsAsync(role);
                    if (existingClaims.Any(existing =>
                            existing.Type == claim.Type && existing.Value == claim.Value))
                        return SeedOperationResult.Success();

                    return IdentityResultToOutcome(await _roleManager.AddClaimAsync(role, claim));
                });
            }
        }
    }

    private async Task SeedInvitationsAsync(
        SeedGroup group,
        IInvitationService invitationService,
        SeedSummary summary,
        CancellationToken cancellationToken)
    {
        if (group.Options.Invitations.Count == 0)
            return;

        foreach (var options in group.Options.Invitations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = options.ConfigurationPath ?? group.Path;
            await RunOperationAsync(group.Name, path, "invitation", "configured invitation", summary, cancellationToken, async () =>
            {
                if (options.ConfigurationError is not null)
                    return SeedOperationResult.Failure(options.ConfigurationError);
                if (!Guid.TryParse(options.Code, out var code))
                    return SeedOperationResult.Failure("Invitation code must be a valid GUID.");
                if (code == Guid.Empty)
                    return SeedOperationResult.Skipped("Empty invitation code is not allowed.");
                if (options.ExpiresIn is { } expiresIn && expiresIn < TimeSpan.Zero)
                    return SeedOperationResult.Failure("Invitation expiry must not be negative.");
                if (options.Roles.Any(string.IsNullOrWhiteSpace))
                    return SeedOperationResult.Failure("Invitation roles must not contain empty values.");
                if (!TryGetClaims(options.Claims, out var claims, out var error))
                    return SeedOperationResult.Failure(error);

                var codeText = code.ToString("D");
                if (await invitationService.GetByCodeAsync(codeText) is not null)
                    return SeedOperationResult.Success();

                try
                {
                    await invitationService.CreateAsync(
                        email: options.Email,
                        roles: options.Roles,
                        claims: claims,
                        expiresIn: options.ExpiresIn,
                        metadata: options.Metadata,
                        code: code);
                    return SeedOperationResult.Success();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    if (await invitationService.GetByCodeAsync(codeText) is not null)
                        return SeedOperationResult.Success();

                    throw;
                }
            });
        }
    }

    private async Task RunOperationAsync(
        string groupName,
        string configurationPath,
        string itemKind,
        string identifyingKey,
        SeedSummary summary,
        CancellationToken cancellationToken,
        Func<Task<SeedOperationResult>> operation)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var result = await operation();
            cancellationToken.ThrowIfCancellationRequested();
            switch (result.Outcome)
            {
                case SeedOutcome.Succeeded:
                    summary.Succeeded++;
                    LogOperationSucceeded(groupName, itemKind, identifyingKey, configurationPath);
                    break;
                case SeedOutcome.Failed:
                    summary.Failed++;
                    LogOperationFailed(groupName, itemKind, identifyingKey, configurationPath, result.Reason);
                    break;
                case SeedOutcome.Skipped:
                    summary.Skipped++;
                    LogOperationSkipped(groupName, itemKind, identifyingKey, configurationPath, result.Reason);
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            summary.Failed++;
            LogOperationException(groupName, itemKind, identifyingKey, configurationPath, exception.GetType().Name);
        }
    }

    private void RecordConfigurationFailure(
        SeedSummary summary,
        string groupName,
        string path,
        string itemKind,
        string reason)
    {
        summary.Failed++;
        LogConfigurationInvalid(groupName, itemKind, path, reason);
    }

    #region Logger Messages

    [LoggerMessage(1, LogLevel.Warning, "Invitation seeding is configured but IInvitationService is not registered. Configured invitations: {Count}.")]
    private partial void LogInvitationServiceNotRegistered(int count);

    [LoggerMessage(2, LogLevel.Information, "Identity seeding completed. Succeeded: {Succeeded}; failed: {Failed}; skipped: {Skipped}.")]
    private partial void LogSeedingCompleted(int succeeded, int failed, int skipped);

    [LoggerMessage(3, LogLevel.Warning, "Identity seeding partially completed. Succeeded: {Succeeded}; failed: {Failed}; skipped: {Skipped}.")]
    private partial void LogSeedingPartiallyCompleted(int succeeded, int failed, int skipped);

    [LoggerMessage(4, LogLevel.Debug, "Identity seed operation succeeded. Group: {GroupName}; item: {ItemKind}; key: {IdentifyingKey}; path: {ConfigurationPath}.")]
    private partial void LogOperationSucceeded(string groupName, string itemKind, string identifyingKey, string configurationPath);

    [LoggerMessage(5, LogLevel.Warning, "Identity seed operation failed. Group: {GroupName}; item: {ItemKind}; key: {IdentifyingKey}; path: {ConfigurationPath}; reason: {Reason}.")]
    private partial void LogOperationFailed(string groupName, string itemKind, string identifyingKey, string configurationPath, string? reason);

    [LoggerMessage(6, LogLevel.Warning, "Identity seed operation skipped. Group: {GroupName}; item: {ItemKind}; key: {IdentifyingKey}; path: {ConfigurationPath}; reason: {Reason}.")]
    private partial void LogOperationSkipped(string groupName, string itemKind, string identifyingKey, string configurationPath, string? reason);

    [LoggerMessage(7, LogLevel.Error, "Identity seed operation threw an exception. Group: {GroupName}; item: {ItemKind}; key: {IdentifyingKey}; path: {ConfigurationPath}; exception type: {ExceptionType}.")]
    private partial void LogOperationException(string groupName, string itemKind, string identifyingKey, string configurationPath, string exceptionType);

    [LoggerMessage(8, LogLevel.Warning, "Identity seed configuration is invalid. Group: {GroupName}; item: {ItemKind}; path: {ConfigurationPath}; reason: {Reason}.")]
    private partial void LogConfigurationInvalid(string groupName, string itemKind, string configurationPath, string reason);

    #endregion

    private static SeedOperationResult IdentityResultToOutcome(IdentityResult result)
    {
        return result.Succeeded
            ? SeedOperationResult.Success()
            : SeedOperationResult.Failure($"Identity operation returned {result.Errors.Count()} error(s).");
    }

    private static bool TryGetClaim(
        SeedClaimOptions options,
        out Claim claim,
        out string error)
    {
        claim = null!;
        if (string.IsNullOrWhiteSpace(options.Type))
        {
            error = "Claim type is required.";
            return false;
        }

        if (options.Value is null)
        {
            error = "Claim value is required.";
            return false;
        }

        claim = new Claim(options.Type, options.Value);
        error = string.Empty;
        return true;
    }

    private static bool TryGetClaims(
        IReadOnlyList<SeedClaimOptions> options,
        out IReadOnlyList<ClaimInfo> claims,
        out string error)
    {
        var result = new List<ClaimInfo>(options.Count);
        foreach (var claim in options)
        {
            if (claim.ConfigurationError is not null)
            {
                claims = Array.Empty<ClaimInfo>();
                error = claim.ConfigurationError;
                return false;
            }

            if (string.IsNullOrWhiteSpace(claim.Type) || claim.Value is null)
            {
                claims = Array.Empty<ClaimInfo>();
                error = "Invitation claim requires a type and value.";
                return false;
            }

            result.Add(new ClaimInfo { Type = claim.Type, Value = claim.Value });
        }

        claims = result;
        error = string.Empty;
        return true;
    }

    private static string SafeKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(unspecified)" : value;
    }

    private static IEnumerable<SeedGroup> ReadGroups(IConfigurationSection section)
    {
        foreach (var groupSection in section.GetChildren())
        {
            var options = new IdentitySeederGroupOptions
            {
                GroupName = groupSection.Key,
                ConfigurationPath = groupSection.Path
            };

            var order = groupSection["Order"];
            if (!string.IsNullOrWhiteSpace(order))
            {
                if (int.TryParse(order, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedOrder))
                    options.Order = parsedOrder;
                else
                    options.ConfigurationError = "Group Order must be an integer.";
            }

            foreach (var roleSection in groupSection.GetSection("Roles").GetChildren())
            {
                options.Roles.Add(new SeedRoleOptions
                {
                    Id = roleSection["Id"],
                    Name = roleSection["Name"],
                    ConfigurationPath = roleSection.Path
                });
            }

            foreach (var userSection in groupSection.GetSection("Users").GetChildren())
            {
                var user = new SeedUserOptions
                {
                    UserName = userSection["UserName"],
                    Email = userSection["Email"],
                    Password = userSection["Password"],
                    ConfigurationPath = userSection.Path
                };

                var emailConfirmed = userSection["EmailConfirmed"];
                if (!string.IsNullOrWhiteSpace(emailConfirmed))
                {
                    if (bool.TryParse(emailConfirmed, out var parsedEmailConfirmed))
                        user.EmailConfirmed = parsedEmailConfirmed;
                    else
                        user.ConfigurationError = "EmailConfirmed must be a boolean.";
                }

                foreach (var roleSection in userSection.GetSection("Roles").GetChildren())
                {
                    user.Roles.Add(roleSection.Value ?? string.Empty);
                    user.RoleConfigurationPaths.Add(roleSection.Path);
                }

                user.Claims.AddRange(ReadClaims(userSection.GetSection("Claims")));
                options.Users.Add(user);
            }

            foreach (var roleClaimsSection in groupSection.GetSection("RoleClaims").GetChildren())
            {
                var roleClaims = new SeedRoleClaimsOptions
                {
                    Role = roleClaimsSection["Role"],
                    ConfigurationPath = roleClaimsSection.Path
                };
                roleClaims.Claims.AddRange(ReadClaims(roleClaimsSection.GetSection("Claims")));
                options.RoleClaims.Add(roleClaims);
            }

            foreach (var invitationSection in groupSection.GetSection("Invitations").GetChildren())
            {
                var invitation = new SeedInvitationOptions
                {
                    Code = invitationSection["Code"],
                    Email = invitationSection["Email"],
                    Metadata = invitationSection["Metadata"],
                    ConfigurationPath = invitationSection.Path
                };
                invitation.Roles.AddRange(ReadStrings(invitationSection.GetSection("Roles")));

                var expiresIn = invitationSection["ExpiresIn"];
                if (!string.IsNullOrWhiteSpace(expiresIn))
                {
                    if (TimeSpan.TryParse(expiresIn, CultureInfo.InvariantCulture, out var parsedExpiresIn))
                        invitation.ExpiresIn = parsedExpiresIn;
                    else
                        invitation.ConfigurationError = "ExpiresIn must be a valid TimeSpan.";
                }

                invitation.Claims.AddRange(ReadClaims(invitationSection.GetSection("Claims")));
                options.Invitations.Add(invitation);
            }

            yield return new SeedGroup(groupSection.Key, options, groupSection.Path);
        }
    }

    private static IEnumerable<string> ReadStrings(IConfigurationSection section)
    {
        return section.GetChildren().Select(child => child.Value ?? string.Empty);
    }

    private static IEnumerable<SeedClaimOptions> ReadClaims(IConfigurationSection section)
    {
        foreach (var claimSection in section.GetChildren())
        {
            yield return new SeedClaimOptions
            {
                Type = claimSection["Type"],
                Value = claimSection["Value"],
                ConfigurationPath = claimSection.Path
            };
        }
    }

    private sealed record SeedGroup(
        string Name,
        IdentitySeederGroupOptions Options,
        string Path);

    private sealed class SeedSummary
    {
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
    }

    private enum SeedOutcome
    {
        Succeeded,
        Failed,
        Skipped
    }

    private sealed record SeedOperationResult(SeedOutcome Outcome, string? Reason = null)
    {
        public static SeedOperationResult Success() => new(SeedOutcome.Succeeded);
        public static SeedOperationResult Failure(string reason) => new(SeedOutcome.Failed, reason);
        public static SeedOperationResult Skipped(string reason) => new(SeedOutcome.Skipped, reason);
    }
}

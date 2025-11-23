namespace NuxtIdentity.Tenancy.Abstractions;

/// <summary>
/// Marker interface for resources that belong to a tenant.
/// Implement this on your domain models to enable tenant-based authorization.
/// </summary>
public interface ITenantResource
{
    Guid TenantId { get; }
}

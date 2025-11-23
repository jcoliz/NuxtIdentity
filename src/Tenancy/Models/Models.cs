using NuxtIdentity.Core.Models;

namespace NuxtIdentity.Tenancy.Models;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class UserTenantRole  
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public TenantRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public Tenant Tenant { get; set; } = null!;
}

public enum TenantRole
{
    Viewer = 1,
    Editor = 2, 
    Owner = 3
}

public record TenantInfo
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public TenantRole Role { get; init; }
}
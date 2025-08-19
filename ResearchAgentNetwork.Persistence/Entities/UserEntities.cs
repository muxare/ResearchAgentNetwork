namespace ResearchAgentNetwork.Persistence.Entities;

public class UserEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
    public string? SecurityStamp { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<UserRoleEntity> Roles { get; set; } = new List<UserRoleEntity>();
    public ICollection<RefreshTokenEntity> RefreshTokens { get; set; } = new List<RefreshTokenEntity>();
}

public class RoleEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;

    public ICollection<UserRoleEntity> Users { get; set; } = new List<UserRoleEntity>();
}

public class UserRoleEntity
{
    public Guid UserId { get; set; }
    public UserEntity User { get; set; } = null!;
    public Guid RoleId { get; set; }
    public RoleEntity Role { get; set; } = null!;
}

public class RefreshTokenEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public UserEntity User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByToken { get; set; }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}


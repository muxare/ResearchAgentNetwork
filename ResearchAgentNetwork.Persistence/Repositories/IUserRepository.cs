using ResearchAgentNetwork.Persistence.Entities;

namespace ResearchAgentNetwork.Persistence.Repositories;

public interface IUserRepository
{
	Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<UserEntity?> GetByUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default);
	Task<UserEntity> CreateAsync(UserEntity user, IEnumerable<string> roles, CancellationToken cancellationToken = default);
	Task UpdateAsync(UserEntity user, IEnumerable<string>? roles = null, CancellationToken cancellationToken = default);
	Task<bool> ExistsByUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default);
	Task<RoleEntity> EnsureRoleAsync(string roleName, CancellationToken cancellationToken = default);
}


using Microsoft.EntityFrameworkCore;
using ResearchAgentNetwork.Persistence.Entities;

namespace ResearchAgentNetwork.Persistence.Repositories;

public class UserRepository : IUserRepository
{
	private readonly AppDbContext _db;

	public UserRepository(AppDbContext db)
	{
		_db = db;
	}

	public async Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return await _db.Users
			.Include(u => u.Roles).ThenInclude(ur => ur.Role)
			.Include(u => u.RefreshTokens)
			.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
	}

	public async Task<UserEntity?> GetByUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
	{
		return await _db.Users
			.Include(u => u.Roles).ThenInclude(ur => ur.Role)
			.Include(u => u.RefreshTokens)
			.FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, cancellationToken);
	}

	public async Task<UserEntity> CreateAsync(UserEntity user, IEnumerable<string> roles, CancellationToken cancellationToken = default)
	{
		// Normalize
		user.NormalizedUserName = user.UserName.ToUpperInvariant();
		user.NormalizedEmail = string.IsNullOrWhiteSpace(user.Email) ? string.Empty : user.Email.ToUpperInvariant();
		user.CreatedAtUtc = DateTime.UtcNow;

		_db.Users.Add(user);
		await _db.SaveChangesAsync(cancellationToken);

		if (roles != null)
		{
			foreach (var roleName in roles)
			{
				var role = await EnsureRoleAsync(roleName, cancellationToken);
				_db.UserRoles.Add(new UserRoleEntity { UserId = user.Id, RoleId = role.Id });
			}
			await _db.SaveChangesAsync(cancellationToken);
		}

		return user;
	}

	public async Task UpdateAsync(UserEntity user, IEnumerable<string>? roles = null, CancellationToken cancellationToken = default)
	{
		user.UpdatedAtUtc = DateTime.UtcNow;
		user.NormalizedUserName = user.UserName.ToUpperInvariant();
		user.NormalizedEmail = string.IsNullOrWhiteSpace(user.Email) ? string.Empty : user.Email.ToUpperInvariant();
		_db.Users.Update(user);
		await _db.SaveChangesAsync(cancellationToken);

		if (roles != null)
		{
			var userRoleRows = await _db.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync(cancellationToken);
			_db.UserRoles.RemoveRange(userRoleRows);
			foreach (var roleName in roles)
			{
				var role = await EnsureRoleAsync(roleName, cancellationToken);
				_db.UserRoles.Add(new UserRoleEntity { UserId = user.Id, RoleId = role.Id });
			}
			await _db.SaveChangesAsync(cancellationToken);
		}
	}

	public async Task<bool> ExistsByUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
	{
		return await _db.Users.AnyAsync(u => u.NormalizedUserName == normalizedUserName, cancellationToken);
	}

	public async Task<RoleEntity> EnsureRoleAsync(string roleName, CancellationToken cancellationToken = default)
	{
		var normalized = roleName.ToUpperInvariant();
		var role = await _db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == normalized, cancellationToken);
		if (role != null) return role;
		role = new RoleEntity { Name = roleName, NormalizedName = normalized };
		_db.Roles.Add(role);
		await _db.SaveChangesAsync(cancellationToken);
		return role;
	}
}


# User Management Persistence

This implements user management at the persistence layer with Entity Framework Core, including users, roles, user-role assignments, and refresh tokens.

## Schema

- Users (UserEntity)
  - Id (GUID PK)
  - UserName, NormalizedUserName (unique), Email, NormalizedEmail
  - EmailConfirmed, PasswordHash, DisplayName, IsActive, SecurityStamp
  - CreatedAtUtc, UpdatedAtUtc
- Roles (RoleEntity)
  - Id (GUID PK)
  - Name, NormalizedName (unique)
- UserRoles (UserRoleEntity)
  - Composite PK (UserId, RoleId)
  - FKs to Users and Roles (cascade on delete)
- RefreshTokens (RefreshTokenEntity)
  - Id (GUID PK)
  - UserId (FK), Token (unique per user), CreatedAtUtc, ExpiresAtUtc, RevokedAtUtc, ReplacedByToken

Indexes:
- Users.NormalizedUserName unique
- Users.NormalizedEmail
- Roles.NormalizedName unique
- RefreshTokens (UserId, Token) unique

## Code Locations

- Entities: `ResearchAgentNetwork.Persistence/Entities/UserEntities.cs`
- DbSets & model config: `ResearchAgentNetwork.Persistence/AppDbContext.cs`
- Repository interface: `ResearchAgentNetwork.Persistence/Repositories/IUserRepository.cs`
- Repository implementation: `ResearchAgentNetwork.Persistence/Repositories/UserRepository.cs`
- DI registration: `ResearchAgentNetwork.Web/Program.cs`

## Data Flow

- Application code resolves `IUserRepository` from DI to perform CRUD operations.
- Role creation is centralized via `EnsureRoleAsync` to avoid duplicates.
- Token persistence uses `RefreshTokenEntity` for rotation and revocation tracking.

## How to Apply / Migrate

The web app applies EF migrations on startup.

- Migration: `AddUsers` under `ResearchAgentNetwork.Persistence/Migrations/`.
- Apply: run the web project or execute

```bash
# from repo root
pwsh -c "dotnet ef database update --project ResearchAgentNetwork.Persistence --startup-project ResearchAgentNetwork.Web"
```

## Example Usage

```csharp
var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

// Create user with role
var created = await users.CreateAsync(new UserEntity
{
    UserName = "alice",
    Email = "alice@example.com",
    PasswordHash = BCrypt.Net.BCrypt.HashPassword("P@ssw0rd")
}, roles: new[] { "Admin" });

// Fetch
var u = await users.GetByUserNameAsync("ALICE");

// Update + roles
u!.DisplayName = "Alice";
await users.UpdateAsync(u, roles: new[] { "User", "Researcher" });
```

Note: password hashing/verification and issuing tokens are handled at the application/auth layer.

## Next Steps (optional)

- Add minimal API endpoints for register/login/refresh/logout using `IUserRepository`.
- Introduce JWT issuance & validation, middleware for authorization, and role-based policies.
- Add seed data for initial admin user and roles via startup.

## Auth Endpoints (implemented)

- POST `/api/auth/register` → create user
- POST `/api/auth/login` → returns `{ accessToken, refreshToken, roles }`
- POST `/api/auth/refresh` → returns new `{ accessToken }` from refresh token
- POST `/api/auth/logout` → revokes given refresh token

Config values under `Jwt` in `ResearchAgentNetwork.Web/appsettings.json`:

```json
{
  "Jwt": {
    "Issuer": "ran.local",
    "Audience": "ran.clients",
    "Key": "dev_insecure_key_change_me",
    "AccessTokenMinutes": 30,
    "RefreshTokenDays": 14
  }
}
```

### How to Test (curl)

```bash
# register
curl -s -X POST http://localhost:5223/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"userName":"alice","email":"alice@example.com","password":"P@ssw0rd"}'

# login
curl -s -X POST http://localhost:5223/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"alice","password":"P@ssw0rd"}'

# refresh
curl -s -X POST http://localhost:5223/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<paste token>"}'

# logout
curl -s -X POST http://localhost:5223/api/auth/logout \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<paste token>"}'
```
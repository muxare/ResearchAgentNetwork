### Database Persistence Setup (MSSQL or SQLite)

This app now supports SQL Server (recommended) and SQLite (fallback) for persisting tasks, events, and reports.

### Configure Provider

Edit `ResearchAgentNetwork.Web/appsettings.json`:

```json
{
  "Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=localhost;Database=ResearchAgentNetwork;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

- Set `Provider` to `SqlServer` for MSSQL, or `Sqlite` to use the local `ran.db` file.
- For MSSQL with SQL auth use: `Server=localhost;Database=ResearchAgentNetwork;User Id=sa;Password=yourStrong(!)Password;TrustServerCertificate=True;`

### Packages

`ResearchAgentNetwork.Persistence` references both providers:
- `Microsoft.EntityFrameworkCore.SqlServer`
- `Microsoft.EntityFrameworkCore.Sqlite`

### Startup Behavior

- On startup, the app calls `EnsureCreated()` to create the schema if missing.
- A small SQLite-only migration shim adds columns to `TaskEvents` if needed.

### Entities and Tables

- `Tasks` (`TaskEntity`): basic task snapshot
- `TaskEvents` (`TaskEventEntity`): timeline of events
- `TaskReports` (`TaskReportEntity`): latest generated report markdown per task
- `Users` (`UserEntity`): application users with normalized username/email, password hash, status
- `Roles` (`RoleEntity`): role catalog with unique normalized name
- `UserRoles` (`UserRoleEntity`): many-to-many join for user-role assignments
- `RefreshTokens` (`RefreshTokenEntity`): persisted refresh tokens with expiry/revocation

### How to Test

1. Start your local SQL Server (Developer/Express). Ensure you can connect to `localhost`.
2. Update the connection string in `appsettings.json` if needed.
3. Run the web app from `ResearchAgentNetwork.Web`.
4. Verify DB info: GET `/api/dbinfo`.
   - For SqlServer, you should see `provider: SqlServer` and the connection string echoed.
5. Create a task: POST `/api/tasks` with `{ "description": "test", "priority": 5 }`.
6. Observe events: GET `/api/tasks/{id}/events` or stream `/api/events`.
7. Check SQL Server: you should see `Tasks`, `TaskEvents`, and `TaskReports` populated.
8. For user tables: verify presence of `Users`, `Roles`, `UserRoles`, `RefreshTokens`.

### User Management Persistence

- DI registrations are in `ResearchAgentNetwork.Web/Program.cs` for `IUserRepository` → `UserRepository`.
- Repositories are in `ResearchAgentNetwork.Persistence/Repositories/`.
- Entities are in `ResearchAgentNetwork.Persistence/Entities/UserEntities.cs`.

#### Sample usage (C#)

```csharp
// resolve from DI inside a minimal API or service
var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

// create user with roles
var created = await users.CreateAsync(new UserEntity
{
    UserName = "alice",
    Email = "alice@example.com",
    PasswordHash = BCrypt.Net.BCrypt.HashPassword("P@ssw0rd")
}, roles: new[] { "Admin" });

// lookup by normalized username
var u = await users.GetByUserNameAsync("ALICE");

// update and set roles
u!.DisplayName = "Alice";
await users.UpdateAsync(u, roles: new[] { "User", "Researcher" });
```

Note: Hashing/verification and auth flows are application concerns and not handled at the repository layer.

### Architecture & Data Flow

- The `ResearchOrchestrator` emits task events.
- A background subscriber persists snapshots to the database on each event.
- When a task completes/fails, the latest report is stored in `TaskReports`.

### Next Steps (Tracked in TODO.md)

- Replace `EnsureCreated()` with proper EF Core migrations.
- Add repositories (`ITaskRepository`, `IEventRepository`, `IResultRepository`).
- Persist checkpointed execution state for advanced recovery features.


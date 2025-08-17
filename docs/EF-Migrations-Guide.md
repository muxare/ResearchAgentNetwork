### EF Core Migrations Guide

This project uses EF Core migrations with the Web project as the startup app and the Persistence project holding the DbContext.

### Commands

- Create a migration:
```bash
dotnet ef migrations add <Name> -p ./ResearchAgentNetwork.Persistence/ResearchAgentNetwork.Persistence.csproj -s ./ResearchAgentNetwork.Web/ResearchAgentNetwork.Web.csproj -o Migrations -c ResearchAgentNetwork.Persistence.AppDbContext
```

- Update database to latest:
```bash
dotnet ef database update -p ./ResearchAgentNetwork.Persistence/ResearchAgentNetwork.Persistence.csproj -s ./ResearchAgentNetwork.Web/ResearchAgentNetwork.Web.csproj
```

- Update to a specific migration:
```bash
dotnet ef database update <MigrationName> -p ./ResearchAgentNetwork.Persistence/ResearchAgentNetwork.Persistence.csproj -s ./ResearchAgentNetwork.Web/ResearchAgentNetwork.Web.csproj
```

- Roll back one migration:
```bash
dotnet ef database update <PreviousMigration> -p ./ResearchAgentNetwork.Persistence/ResearchAgentNetwork.Persistence.csproj -s ./ResearchAgentNetwork.Web/ResearchAgentNetwork.Web.csproj
```

- Remove the last (unapplied) migration:
```bash
dotnet ef migrations remove -p ./ResearchAgentNetwork.Persistence/ResearchAgentNetwork.Persistence.csproj -s ./ResearchAgentNetwork.Web/ResearchAgentNetwork.Web.csproj
```

### Provider notes

- SQL Server: controlled via `ResearchAgentNetwork.Web/appsettings.json` `Database:Provider = "SqlServer"` and `Database:ConnectionString`.
- SQLite: set `Database:Provider = "Sqlite"`. Migrations will target SQLite when active.

### Troubleshooting

- Tables already exist: drop the tables or the DB, then run `database update`.
- Migrations history mismatch: drop `__EFMigrationsHistory` or baseline using an empty migration (`add <Name> --ignore-changes`), then continue.
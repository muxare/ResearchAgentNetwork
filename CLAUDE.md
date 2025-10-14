# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Research Agent Network is a multi-agent research assistant built on Microsoft Semantic Kernel. It decomposes complex research tasks into atomic subtasks, executes them with LLM agents, assesses quality, and aggregates results into a final synthesis.

- **.NET 9 SDK** required
- Supports multiple LLM providers: **Ollama** (default, local), **OpenAI**, or **Azure OpenAI**
- Optional vector database: **Qdrant** for semantic memory and task deduplication
- Optional web search: **Tavily API** for real-time information retrieval

## Build and Test Commands

```bash
# Build the solution
dotnet build

# Run tests (LLM integration tests auto-skip if Ollama unavailable)
dotnet test

# Run console application
dotnet run --project ResearchAgentNetwork.ConsoleApp

# Run web API + UI
dotnet run --project ResearchAgentNetwork.Web
```

## Frontend Development

Two UI implementations are available:

### React Router UI (ui-react-router/)
```bash
cd ui-react-router
npm install
npm run dev        # Development server
npm run build      # Production build
```

### SvelteKit UI (ui/app/)
```bash
# Build and deploy to wwwroot/v2
pwsh ./build-ui.ps1

# Build to custom path
pwsh ./build-ui.ps1 -BasePath /v3

# Skip npm install (if dependencies already installed)
pwsh ./build-ui.ps1 -SkipInstall

# Force clean rebuild (resolves Windows file locks)
pwsh ./build-ui.ps1 -ForceClean
```

## Architecture

The solution follows Clean Architecture principles with clear separation of concerns:

### Project Structure

- **ResearchAgentNetwork.Core**: Domain models, agent interfaces, and orchestration logic
  - `Domain/`: Core entities (ResearchTask, ResearchResult, TaskStatus, QualityAssessment, etc.)
  - `Agents/`: LLM-powered agents (TaskAnalyzerAgent, ExecutorAgent, AggregatorAgent, QualityAssessmentAgent, WebSearchAgent, etc.)
  - `Orchestration/`: ResearchOrchestrator manages queues, concurrency, and agent pipeline
  - `Common/`: Cross-cutting utilities (KernelExtensions for structured outputs, JSON helpers)

- **ResearchAgentNetwork.Infrastructure**: External service implementations
  - `AIProviders/`: Provider-agnostic kernel configuration (OpenAI, AzureOpenAI, Ollama)
  - `SemanticMemory/`: Vector store adapters (InMemory, Qdrant via SK)
  - `WebSearch/`: Web search service implementations (Tavily, rate limiting, allowlisting)

- **ResearchAgentNetwork.Persistence**: Data access layer
  - `Entities/`: EF Core entities for tasks, events, reports, users
  - `Repositories/`: Repository pattern implementations
  - `AppDbContext.cs`: EF Core DbContext with support for SQLite (default) and SQL Server
  - `Migrations/`: EF Core migrations (auto-applied on startup)

- **ResearchAgentNetwork.Web**: ASP.NET Core minimal API + static file hosting
  - REST API endpoints for task submission, status, reports
  - Server-Sent Events (SSE) for real-time progress updates
  - JWT authentication with refresh tokens
  - Static file hosting for React Router and SvelteKit UIs

- **ResearchAgentNetwork.ConsoleApp**: Console runner for batch processing

### Agent Pipeline Flow

1. **Task Submission** → `ResearchOrchestrator.SubmitResearchTask()` → enqueues task
2. **Task Processing Loop** (concurrent with semaphore throttling):
   - **TaskAnalyzerAgent**: Analyzes complexity; decides to decompose or execute atomically
   - **TaskMergerAgent**: Checks for similar pending/completed tasks; merges or reuses results
   - **ExecutorAgent**: Executes atomic task; produces content + sources
   - **QualityAssessmentAgent**: Evaluates result quality; generates follow-up tasks if needed
   - **AggregatorAgent**: Synthesizes child results into parent report when all children complete
3. **Finalization Pipeline** (for root tasks after all research complete):
   - **ReportOutlineAgent**: Generates structured outline
   - **SectionWriterAgent**: Drafts each section with citations
   - **FactCheckAgent**: Validates claims against evidence
   - **CitationManagerAgent**: Formats citations and bibliography

### Key Design Patterns

- **Structured Outputs**: All agents use `KernelExtensions.GetStructuredResponseAsync<T>()` with JSON schema guidance for reliable LLM parsing
- **Resilient Parsing**: Robust fallbacks for LLM variations (handles arrays of strings/objects, nested structures)
- **Event-Driven**: `TaskEventPublished` events broadcast task state changes to UI via SSE
- **Repository Pattern**: Data access abstracted through interfaces (`ITaskRepository`, `IEventRepository`, etc.)
- **Provider Factory**: `AIProviderFactory.CreateProvider()` selects LLM provider based on configuration

## Configuration

Configuration is loaded from `appsettings.json` and environment variables (env vars override).

### AI Provider Selection
```json
{
  "AI_PROVIDER": "Ollama",  // or "OpenAI", "AzureOpenAI"
  "Ollama": {
    "ModelId": "llama3.1:latest",
    "Endpoint": "http://localhost:11434",
    "EmbeddingModelId": "nomic-embed-text"
  },
  "OpenAI": {
    "ModelId": "gpt-4o-mini",
    "ApiKey": "your-openai-api-key",
    "EmbeddingModelId": "text-embedding-3-small"
  }
}
```

### Orchestrator Settings
```json
{
  "ResearchAgent": {
    "MaxConcurrency": 5,                  // Parallel task execution limit
    "MaxDecompositionDepth": 2,           // Max task tree depth
    "MaxRetries": 1,                      // Retry failed tasks
    "LogPrompts": true,                   // Log LLM prompts/responses
    "EnableWebSearch": false,             // Enable WebSearchAgent
    "Merging": {
      "PendingThreshold": 0.9,            // Similarity threshold for merging pending tasks
      "CompletedThreshold": 0.95          // Similarity threshold for reusing completed results
    },
    "Rag": {
      "StoreMinConfidence": 0.6,          // Min confidence to store result in vector memory
      "DuplicateThreshold": 0.98          // Similarity threshold for duplicate detection
    }
  }
}
```

### Database Configuration
```json
{
  "Database": {
    "Provider": "Sqlite",                 // or "SqlServer"
    "ConnectionString": "..."             // Only for SqlServer
  }
}
```

### Vector Database (Optional)
```json
{
  "VectorDb": {
    "Provider": "Qdrant",                 // or "None" to disable
    "Endpoint": "localhost:6334",         // gRPC endpoint
    "CollectionPrefix": "ran",
    "TopK": 5                             // Number of similar results to retrieve
  }
}
```

### Web Search (Optional)
```json
{
  "WebSearch": {
    "Provider": "TavilyApi",              // or "None" to disable
    "Tavily": {
      "ApiKey": "your-tavily-api-key"
    },
    "Allowlist": "arxiv.org,wikipedia.org",  // Comma-separated domains
    "RateLimit": {
      "RPM": 30,                          // Requests per minute
      "MinIntervalMs": 500                // Min milliseconds between requests
    }
  }
}
```

## Working with Database Migrations

```bash
# Add a new migration (from solution root)
dotnet ef migrations add MigrationName --project ResearchAgentNetwork.Persistence --startup-project ResearchAgentNetwork.Web

# Apply migrations (automatic on Web startup, or manual)
dotnet ef database update --project ResearchAgentNetwork.Persistence --startup-project ResearchAgentNetwork.Web

# Generate SQL script for migration
dotnet ef migrations script --project ResearchAgentNetwork.Persistence --startup-project ResearchAgentNetwork.Web
```

## Web API Endpoints

### Task Management
- `POST /api/tasks` - Submit new research task
- `GET /api/tasks/{id}` - Get task status
- `GET /api/tasks/{id}/children` - Get child tasks
- `GET /api/tasks` - List all tasks (orchestrator snapshot)
- `GET /api/tasks/root?top=100&skip=0` - Get root tasks (from persistence)
- `PATCH /api/tasks/{id}` - Task actions (cancel, retry, force)
- `GET /api/tasks/{id}/events?includeChildren=true` - Get task timeline

### Reports
- `GET /api/tasks/{id}/report` - Generate report (plain text markdown)
- `GET /api/reports/{id}` - Get persisted report
- `GET /api/reports/{id}/download?format=md` - Download report

### Real-time Updates
- `GET /api/events` - SSE stream of task events and progress

### Settings
- `GET /api/settings` - Get runtime settings
- `POST /api/settings` - Update settings (MaxDecompositionDepth, LogPrompts)

### Admin
- `GET /admin/tasks?status=...&q=...&fromUtc=...&toUtc=...` - Query tasks with filters
- `GET /admin/events?taskId=...&fromUtc=...&toUtc=...` - Query events

### Authentication (JWT)
- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Login (returns access token + refresh token)
- `POST /api/auth/refresh` - Refresh access token
- `POST /api/auth/logout` - Revoke refresh token

## Common Development Scenarios

### Adding a New Agent

1. Create agent class in `ResearchAgentNetwork.Core/Agents/`
2. Implement `IResearchAgent` interface or create specialized agent
3. Define response model (record with `Description` attributes for JSON schema)
4. Use `KernelExtensions.GetStructuredResponseAsync<T>()` for LLM calls
5. Register agent in `ResearchOrchestrator.InitializeAgents()` if needed
6. Add agent to pipeline in `ResearchOrchestrator.ProcessTaskAsync()`

### Modifying Task Processing Logic

Key file: `ResearchAgentNetwork.Core/Orchestration/ResearchOrchestrator.cs`

- Task queue management: `_taskQueue` and `_taskRegistry`
- Concurrency control: `_throttle` semaphore
- Agent pipeline: `ProcessTaskAsync()` method
- Event publishing: `Publish()` broadcasts `TaskEvent` to subscribers

### Adding a New LLM Provider

1. Create provider class in `ResearchAgentNetwork.Infrastructure/AIProviders/`
2. Implement `IAIProvider` interface
3. Add provider to `AIProviderFactory.CreateProvider()` switch
4. Update `appsettings.json` with provider configuration
5. Test with `dotnet test` and console app

### Extending the Data Model

1. Add/modify entity in `ResearchAgentNetwork.Persistence/Entities/`
2. Update `AppDbContext.cs` if new entity
3. Create migration: `dotnet ef migrations add YourMigration --project ResearchAgentNetwork.Persistence --startup-project ResearchAgentNetwork.Web`
4. Test migration on both SQLite and SQL Server if possible
5. Update repositories/APIs as needed

## Documentation References

- `docs/ResearchAgentNetwork-Architecture.md` - Detailed architecture overview
- `docs/ResearchAgentNetwork-Implementation.md` - Implementation details
- `docs/Finalization-Pipeline.md` - Report generation pipeline
- `docs/EF-Migrations-Guide.md` - Database migration procedures
- `docs/RAG-Implementation-Plan.md` - Retrieval-Augmented Generation design
- `docs/WebSearch-*.md` - Web search integration guides

## Important Implementation Notes

- **Namespace**: All code uses `ResearchAgentNetwork` namespace (no project-specific namespaces)
- **Structured Outputs**: Always use `KernelExtensions.GetStructuredResponseAsync<T>()` for LLM calls; include `Description` attributes on model properties for schema guidance
- **Event Broadcasting**: Use `Publish(new TaskEvent {...})` to notify UI/persistence layer of state changes
- **Task Categories**: `Normal` (user tasks), `Atomic` (leaf execution), `Aggregation` (parent synthesis), `FollowUp` (quality improvement), `Finalization` (report generation)
- **Metadata Dictionary**: Use `ResearchTask.Metadata` for agent-specific data (e.g., `"FinalizationStep"`, `"DependsOn"`, `"RetryAttempt"`)
- **Resilient LLM Parsing**: Agents should handle LLM output variations (arrays vs objects, nested structures, missing fields)
- **Prompt Logging**: When `LogPrompts` is enabled, `KernelExtensions` logs all prompts/responses via `ILogger`

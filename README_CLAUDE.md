# Research Agent Network - Quick Start Guide

## What is Research Agent Network?

A multi-agent research assistant with **dual implementations**:
1. **.NET 9 with Microsoft Semantic Kernel** (primary, production-ready)
2. **Python with LangGraph** (parallel learning implementation)

Both implementations decompose complex research tasks into atomic subtasks, execute them with LLM agents, assess quality, and aggregate results into comprehensive reports.

**Important:** The Python/LangGraph implementation is a **learning initiative** to understand both .NET and Python approaches to agent networks. It is NOT intended to replace the .NET system, but rather to serve as a comparative implementation for knowledge building and use case optimization.

## Prerequisites

- **.NET 9 SDK** - Required for backend
- **Node.js 18+** (with npm 9+) - Required for frontend development
- **LLM Provider** (one of):
  - **Ollama** (default, local) - Recommended for development
  - **OpenAI** - Requires API key
  - **Azure OpenAI** - Requires Azure credentials
- **Optional Dependencies**:
  - **Qdrant** (vector database) - For semantic memory and task deduplication
  - **Tavily API** - For web search capabilities
  - **SQL Server** or **SQLite** - For persistence (SQLite is default)

## Solution Architecture

### Backend (.NET)

- **ResearchAgentNetwork.Core** - Domain models, agents, orchestration
- **ResearchAgentNetwork.Infrastructure** - AI providers, vector DB, web search
- **ResearchAgentNetwork.Persistence** - EF Core entities, repositories, migrations
- **ResearchAgentNetwork.Web** - ASP.NET Core minimal API + static file hosting
- **ResearchAgentNetwork.ConsoleApp** - Console runner for batch processing

### Frontend (Two Options)

1. **SvelteKit UI** (`ui/app/`) - Full-featured UI with Kanban board, SSE updates
2. **React Router UI** (`ui-react-router/`) - Modern React UI with Tailwind v4

## Starting the Backend

### 1. Configure LLM Provider

Edit `ResearchAgentNetwork.Web/appsettings.json`:

```json
{
  "AI_PROVIDER": "Ollama",
  "Ollama": {
    "ModelId": "llama3.1:latest",
    "Endpoint": "http://localhost:11434",
    "EmbeddingModelId": "nomic-embed-text"
  }
}
```

Or use environment variables:
```bash
AI_PROVIDER=Ollama
Ollama__ModelId=llama3.1:latest
Ollama__Endpoint=http://localhost:11434
```

### 2. Build the Solution

```bash
# From repository root
dotnet build
```

### 3. Run the Backend API

```bash
# Run web API (default port: http://localhost:5000)
dotnet run --project ResearchAgentNetwork.Web
```

The backend will:
- Apply EF Core migrations automatically
- Start the REST API on port 5000
- Enable SSE (Server-Sent Events) at `/api/events`
- Host static files from `wwwroot/`

### 4. Run Tests (Optional)

```bash
# Run all tests (LLM integration tests auto-skip if Ollama unavailable)
dotnet test
```

### 5. Run Console Application (Alternative)

```bash
# For batch processing without the API
dotnet run --project ResearchAgentNetwork.ConsoleApp
```

## Frontend Development

### Option 1: SvelteKit UI (Recommended)

#### How the SvelteKit Application Works

**Architecture:**
- **Framework**: SvelteKit 2 with TypeScript
- **Styling**: Tailwind CSS v4 (via Vite plugin)
- **Build**: Static export using `@sveltejs/adapter-static`
- **Deployment**: Builds to `ui/app/build/`, then copied to `ResearchAgentNetwork.Web/wwwroot/v2`

**Key Features:**
- Kanban board with drag-and-drop task management
- Real-time updates via Server-Sent Events (SSE)
- Markdown report rendering with `marked`
- Task timeline and event viewer
- Modal-based task details

**Configuration:**
- Base path controlled via `BASE_PATH` environment variable
- Vite dev server proxies `/api` and `/admin` to backend (port 5000)
- Static adapter generates SPA with fallback routing

#### Starting SvelteKit UI

**Development Mode (with hot reload):**

```bash
# Terminal 1: Start backend
cd ResearchAgentNetwork
dotnet run --project ResearchAgentNetwork.Web

# Terminal 2: Start SvelteKit dev server
cd ui/app
npm install
npm run dev
```

Open http://localhost:5173 (Vite dev server with API proxy)

**Production Build (static deployment):**

```bash
# From repository root
pwsh ./build-ui.ps1

# Or with custom base path
pwsh ./build-ui.ps1 -BasePath /v3

# Skip npm install if dependencies already installed
pwsh ./build-ui.ps1 -SkipInstall

# Force clean rebuild (resolves Windows file locks)
pwsh ./build-ui.ps1 -ForceClean
```

The build script:
1. Runs `npm install` or `npm ci` (unless `-SkipInstall`)
2. Builds SvelteKit with `BASE_PATH=/v2`
3. Copies build artifacts to `ResearchAgentNetwork.Web/wwwroot/v2`

After building, the UI is available at: http://localhost:5000/v2

### Option 2: React Router UI

#### How the React Router Application Works

**Architecture:**
- **Framework**: React 19 + React Router v7
- **Build**: Vite + TypeScript
- **Styling**: Tailwind CSS v4 (via `@tailwindcss/vite` plugin)

**Key Features:**
- Task list with status counters
- Task details modal with tabs (overview/report/raw/timeline)
- Report viewer route (`/tasks/:id/report`)
- Live status updates via SSE
- Task actions (retry, cancel, force)

#### Starting React Router UI

```bash
# Terminal 1: Start backend
cd ResearchAgentNetwork
dotnet run --project ResearchAgentNetwork.Web

# Terminal 2: Start React dev server
cd ui-react-router
npm install
npm run dev
```

Open http://localhost:5173

**Production Build:**

```bash
cd ui-react-router
npm run build
```

## Backend API Endpoints

### Task Management
- `POST /api/tasks` - Submit new research task
- `GET /api/tasks` - List all tasks (orchestrator snapshot)
- `GET /api/tasks/root?top=100&skip=0` - Get root tasks (from persistence)
- `GET /api/tasks/{id}` - Get task status
- `GET /api/tasks/{id}/children` - Get child tasks
- `PATCH /api/tasks/{id}` - Task actions (cancel, retry, force)
- `GET /api/tasks/{id}/events?includeChildren=true` - Get task timeline

### Reports
- `GET /api/tasks/{id}/report` - Generate report (markdown)
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
- `POST /api/auth/login` - Login (returns access + refresh tokens)
- `POST /api/auth/refresh` - Refresh access token
- `POST /api/auth/logout` - Revoke refresh token

## Configuration Deep Dive

### Database (appsettings.json)

```json
{
  "Database": {
    "Provider": "Sqlite",  // or "SqlServer"
    "ConnectionString": "..."  // Only for SqlServer
  }
}
```

### Orchestrator Settings

```json
{
  "ResearchAgent": {
    "MaxConcurrency": 5,              // Parallel task execution limit
    "MaxDecompositionDepth": 2,       // Max task tree depth
    "MaxRetries": 1,                  // Retry failed tasks
    "LogPrompts": true,               // Log LLM prompts/responses
    "EnableWebSearch": false,         // Enable WebSearchAgent
    "Merging": {
      "PendingThreshold": 0.9,        // Similarity threshold for merging pending tasks
      "CompletedThreshold": 0.95      // Similarity threshold for reusing completed results
    },
    "Rag": {
      "StoreMinConfidence": 0.6,      // Min confidence to store result in vector memory
      "DuplicateThreshold": 0.98      // Similarity threshold for duplicate detection
    }
  }
}
```

### Vector Database (Optional)

```json
{
  "VectorDb": {
    "Provider": "Qdrant",             // or "None" to disable
    "Endpoint": "localhost:6334",     // gRPC endpoint
    "CollectionPrefix": "ran",
    "TopK": 5                         // Number of similar results to retrieve
  }
}
```

### Web Search (Optional)

```json
{
  "WebSearch": {
    "Provider": "TavilyApi",          // or "None" to disable
    "Tavily": {
      "ApiKey": "your-tavily-api-key"
    },
    "Allowlist": ["arxiv.org", "wikipedia.org"],  // Allowed domains
    "RateLimit": {
      "RPM": 30,                      // Requests per minute
      "MinIntervalMs": 500            // Min milliseconds between requests
    }
  }
}
```

## Common Development Workflows

### Adding Database Migrations

```bash
# Add new migration (from solution root)
dotnet ef migrations add MigrationName \
  --project ResearchAgentNetwork.Persistence \
  --startup-project ResearchAgentNetwork.Web

# Apply migrations (automatic on Web startup, or manual)
dotnet ef database update \
  --project ResearchAgentNetwork.Persistence \
  --startup-project ResearchAgentNetwork.Web
```

### Building Both UIs

```bash
# Build SvelteKit UI to /v2
pwsh ./build-ui.ps1

# Build React Router UI
cd ui-react-router
npm run build

# Copy React build to wwwroot (manual)
Copy-Item -Recurse -Force .\dist\* ..\ResearchAgentNetwork.Web\wwwroot\
```

### Switching LLM Providers

Edit `appsettings.json`:

**For OpenAI:**
```json
{
  "AI_PROVIDER": "OpenAI",
  "OpenAI": {
    "ModelId": "gpt-4o-mini",
    "ApiKey": "your-openai-api-key",
    "EmbeddingModelId": "text-embedding-3-small"
  }
}
```

**For Azure OpenAI:**
```json
{
  "AI_PROVIDER": "AzureOpenAI",
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com",
    "ApiKey": "your-azure-key",
    "DeploymentName": "gpt-4",
    "EmbeddingDeploymentName": "text-embedding-ada-002"
  }
}
```

## How Tasks Flow Through the System

1. **Submit Task** → `POST /api/tasks` → `ResearchOrchestrator.SubmitResearchTask()`
2. **Queue & Process** → Task added to internal queue with priority
3. **Analyzer Agent** → Analyzes complexity; decides to decompose or execute atomically
4. **Merger Agent** → Checks for similar pending/completed tasks; merges or reuses results
5. **Executor Agent** → Executes atomic task; produces content + sources
6. **Quality Assessment Agent** → Evaluates result quality; generates follow-up tasks if needed
7. **Aggregator Agent** → Synthesizes child results into parent report when all children complete
8. **Finalization Pipeline** (for root tasks):
   - **ReportOutlineAgent** → Generates structured outline
   - **SectionWriterAgent** → Drafts each section with citations
   - **FactCheckAgent** → Validates claims against evidence
   - **CitationManagerAgent** → Formats citations and bibliography

## Troubleshooting

### Backend Issues

**Ollama Connection Failed:**
- Ensure Ollama is running: `ollama serve`
- Check endpoint in appsettings.json: `http://localhost:11434`
- Test with: `curl http://localhost:11434/api/tags`

**Database Migration Errors:**
- Delete database and restart (dev only)
- Check connection string
- Manually apply: `dotnet ef database update`

**Port Already in Use:**
- Change port in `ResearchAgentNetwork.Web/Properties/launchSettings.json`
- Or set: `dotnet run --urls "http://localhost:5001"`

### Frontend Issues

**SvelteKit Build Fails:**
- Try force clean: `pwsh ./build-ui.ps1 -ForceClean`
- Check Node version: `node --version` (need 18+)
- Delete `node_modules` and `package-lock.json`, then `npm install`

**Vite Dev Server Proxy Not Working:**
- Ensure backend is running on port 5000
- Check `vite.config.ts` proxy configuration
- Try: `npm run dev -- --host`

**File Lock Errors (Windows):**
- Close VS Code and other editors
- Disable antivirus temporarily
- Use `-ForceClean` flag: `pwsh ./build-ui.ps1 -ForceClean`

## Documentation References

### Core Documentation
- `CLAUDE.md` - Project instructions for Claude Code
- `docs/ResearchAgentNetwork-Architecture.md` - Detailed architecture
- `docs/EF-Migrations-Guide.md` - Database migration procedures
- `docs/Finalization-Pipeline.md` - Report generation pipeline
- `docs/RAG-Implementation-Plan.md` - RAG design

### Frontend Documentation
- `docs/Frontend-UI-Setup.md` - SvelteKit UI setup guide
- `docs/ReactRouter-UI.md` - React Router UI documentation

### Python/LangGraph Learning Track
- `docs/LangGraph-Implementation-Plan-v2.md` - Python/LangGraph implementation blueprint
- `docs/LangGraph-Plan-Improvements.md` - v1 vs v2 improvements
- `docs/SHORT-PLAN.md` - Strategic roadmap for dual-track development
- `docs/LONG-PLAN.md` - Detailed implementation guide
- `docs/COMPARATIVE-ANALYSIS-GUIDE.md` - Framework for comparing .NET vs Python implementations

### Web Search Integration
- `docs/WebSearch-*.md` - Web search integration guides

## Quick Reference

### Backend Commands
```bash
dotnet build                              # Build solution
dotnet test                               # Run tests
dotnet run --project ResearchAgentNetwork.Web  # Start API
dotnet run --project ResearchAgentNetwork.ConsoleApp  # Console mode
```

### SvelteKit Commands
```bash
cd ui/app
npm install                               # Install dependencies
npm run dev                               # Dev server (port 5173)
npm run build                             # Production build
pwsh ../../build-ui.ps1                   # Build + deploy to wwwroot/v2
```

### React Router Commands
```bash
cd ui-react-router
npm install                               # Install dependencies
npm run dev                               # Dev server (port 5173)
npm run build                             # Production build to dist/
```

### Access Points
- Backend API: http://localhost:5000
- SvelteKit Dev: http://localhost:5173
- SvelteKit Prod: http://localhost:5000/v2
- React Router Dev: http://localhost:5173
- API Docs: http://localhost:5000/swagger (if enabled)

---

## Dual-Track Implementation Strategy

### Why Two Implementations?

The ResearchAgentNetwork employs a **dual-track learning strategy** with parallel .NET and Python implementations to:

1. **Deeply Understand Both Ecosystems:** Compare architectural patterns, developer experience, and performance characteristics
2. **Cross-Pollinate Ideas:** Bring LangGraph's streaming and checkpointing concepts to .NET; bring .NET's type safety and enterprise patterns to Python
3. **Optimize Use Cases:** Route each scenario to the most appropriate implementation (latency-sensitive → .NET, complex workflows → Python/LangGraph)
4. **Avoid Vendor Lock-In:** Maintain expertise and production capability in both ecosystems
5. **Maximize Learning:** Direct comparison reveals insights impossible with single implementation

### When to Use Each Implementation

**.NET Semantic Kernel** is recommended for:
- Latency-sensitive applications (< 100ms response requirements)
- Enterprise integration scenarios (Azure, Active Directory, SharePoint)
- High-throughput batch processing
- Type-safety critical applications
- Existing .NET infrastructure and team expertise

**Python LangGraph** is recommended for:
- Complex multi-agent workflows requiring visualization and debugging
- Research and experimentation (faster iteration cycles)
- Advanced streaming requirements
- Integration with Python ML/AI ecosystem (scikit-learn, pandas, TensorFlow)
- Scenarios where LangSmith observability is critical

### Comparative Analysis

For detailed guidance on comparing the two implementations:
- See `docs/COMPARATIVE-ANALYSIS-GUIDE.md` for frameworks and methodologies
- See `docs/SHORT-PLAN.md` for strategic roadmap
- See `docs/LONG-PLAN.md` for detailed implementation steps

### Implementation Status

- **.NET Implementation:** Production-ready, all 15 agents functional
- **Python Implementation:** Planned (see `docs/LangGraph-Implementation-Plan-v2.md`)

---

## Potential Setup & Startup Improvements

This section outlines opportunities to streamline the developer experience and production deployment process.

### 1. Containerization & Orchestration

**Current State:**
- Manual installation of .NET 9, Node.js, Ollama, Qdrant, SQL Server
- Multiple terminal windows required for development
- Platform-specific setup (Windows PowerShell scripts)

**Improvements:**

**Docker Compose for Full Stack:**
```yaml
# docker-compose.yml
services:
  backend:
    build: ./ResearchAgentNetwork.Web
    ports:
      - "5000:8080"
    environment:
      - AI_PROVIDER=Ollama
      - Ollama__Endpoint=http://ollama:11434
    depends_on:
      - ollama
      - qdrant
      - db

  ollama:
    image: ollama/ollama:latest
    ports:
      - "11434:11434"
    volumes:
      - ollama-data:/root/.ollama

  qdrant:
    image: qdrant/qdrant:latest
    ports:
      - "6333:6333"
      - "6334:6334"

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourPassword123!

  frontend:
    build: ./ui/app
    ports:
      - "5173:5173"
    environment:
      - VITE_API_URL=http://localhost:5000
```

**Benefits:**
- Single command startup: `docker-compose up`
- Consistent environment across dev/staging/prod
- Automatic dependency management
- Easy CI/CD integration

### 2. Unified Development Experience

**Current State:**
- Separate commands for backend (`dotnet run`) and frontend (`npm run dev`)
- Manual coordination of startup order
- Two frontend options (SvelteKit and React Router)

**Improvements:**

**Option A: Single CLI Tool**
```bash
# Create a unified development CLI
dotnet tool install --global ran-cli

# Single command to start everything
ran dev                    # Starts backend + frontend in watch mode
ran dev --backend-only     # Backend only
ran dev --frontend=react   # Choose frontend flavor
ran build                  # Build everything for production
ran deploy --env staging   # Deploy to environment
```

**Option B: Cross-Platform Startup Script**
```bash
# ./run.sh or run.ps1 or run.cmd
./run.sh dev               # Start backend + frontend
./run.sh build             # Build all projects
./run.sh test              # Run all tests
./run.sh setup             # Interactive setup wizard
```

**Option C: Task Runner Integration**
```json
// tasks.json for VS Code
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "Start Full Stack",
      "dependsOn": ["Start Backend", "Start Frontend"],
      "problemMatcher": []
    }
  ]
}
```

**Consolidate Frontend:**
- Choose one official UI (recommend SvelteKit for features or React Router for simplicity)
- Move alternative to `examples/` or separate repo
- Reduces confusion and maintenance burden

### 3. Configuration Management

**Current State:**
- `appsettings.json` for backend
- `vite.config.ts` for frontend proxy
- `svelte.config.js` for base path
- Environment variables scattered
- Sensitive data (API keys) in config files

**Improvements:**

**Centralized Configuration:**
```bash
# .env (root level, gitignored)
AI_PROVIDER=Ollama
OLLAMA_ENDPOINT=http://localhost:11434
TAVILY_API_KEY=your-key-here
JWT_SECRET=your-secret-here
DATABASE_CONNECTION=Server=localhost;...

# Or use a configuration service
ran config set ai-provider ollama
ran config set tavily-api-key <key> --encrypt
```

**Environment Profiles:**
```bash
# appsettings.Development.json (auto-loaded)
# appsettings.Production.json (auto-loaded)
# appsettings.Docker.json (for containers)

dotnet run --environment Docker
```

**Secrets Management:**
- Use .NET User Secrets for development: `dotnet user-secrets set "Tavily:ApiKey" "your-key"`
- Azure Key Vault for production
- Docker secrets for containerized deployments
- Never commit secrets to `appsettings.json`

**Configuration Validation on Startup:**
```csharp
// Validate configuration at startup with helpful error messages
public class ConfigurationValidator : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ValidateAIProvider();
        ValidateDatabase();
        ValidateOptionalServices();
        return Task.CompletedTask;
    }
}
```

### 4. Interactive Setup Wizard

**Current State:**
- Manual editing of configuration files
- Trial-and-error for first-time setup
- No validation until runtime

**Improvements:**

**First-Run Setup:**
```bash
# Run on first clone
dotnet run --project ResearchAgentNetwork.Web --setup

# Interactive prompts:
# - Which LLM provider? (Ollama/OpenAI/Azure)
# - Ollama running locally? [Test connection]
# - Enable web search? (requires Tavily API key)
# - Enable vector DB? (requires Qdrant)
# - Which database? (SQLite/SQL Server)
# - Generate sample tasks for testing?

# Generates appsettings.Development.json and user-secrets
```

**Dependency Health Checks:**
```bash
ran doctor                 # Check all dependencies
# ✓ .NET 9.0.1 installed
# ✓ Node.js 20.10.0 installed
# ✓ Ollama running at http://localhost:11434
# ✗ Qdrant not running (optional)
# ✓ Database connection successful
```

### 5. Automated Dependency Installation

**Current State:**
- Manual installation instructions
- Platform-specific (Ollama install differs on Windows/Mac/Linux)

**Improvements:**

**Setup Script:**
```bash
# ./setup.sh or setup.ps1
./setup.sh                 # Detects OS and installs missing dependencies

# Checks and installs:
# - .NET 9 SDK (via dotnet-install)
# - Node.js (via nvm or official installer)
# - Ollama (platform-specific)
# - Pulls default models: ollama pull llama3.1:latest
# - Optionally installs Qdrant via Docker
```

**Development Dependencies as Code:**
```json
// devcontainer.json (for VS Code Remote Containers)
{
  "name": "Research Agent Network",
  "dockerComposeFile": "docker-compose.dev.yml",
  "service": "workspace",
  "features": {
    "dotnet": "9.0",
    "node": "20"
  },
  "postCreateCommand": "dotnet restore && npm install --prefix ui/app"
}
```

### 6. Hot Reload for Full Stack

**Current State:**
- Frontend has hot reload via Vite
- Backend requires manual restart for most changes

**Improvements:**

**Backend Hot Reload:**
```bash
# Use dotnet watch for automatic recompilation
dotnet watch --project ResearchAgentNetwork.Web

# With docker-compose:
volumes:
  - ./ResearchAgentNetwork.Web:/app
command: dotnet watch run
```

**Synchronized Restart:**
- Watch both frontend and backend
- Automatically restart backend when agents or orchestration logic changes
- Preserve database state during restarts (SQLite or docker volume)

### 7. Simplified Production Deployment

**Current State:**
- Manual build steps for frontend
- Manual deployment of backend
- Database migrations need manual attention

**Improvements:**

**Single Build Command:**
```bash
# Build everything for production
./build.sh production

# Output:
# - backend/publish/ (self-contained .NET app)
# - wwwroot/ (merged frontend build)
# - migrations.sql (database migration script)
# - docker-compose.prod.yml (production compose file)
```

**CI/CD Pipeline Example:**
```yaml
# .github/workflows/deploy.yml
- name: Build and Deploy
  run: |
    dotnet publish -c Release
    npm run build --prefix ui/app
    docker build -t ran:latest .
    docker push ran:latest
    kubectl apply -f k8s/
```

**Health Endpoints:**
```csharp
// GET /health
{
  "status": "healthy",
  "checks": {
    "database": "healthy",
    "ollama": "healthy",
    "qdrant": "degraded"  // Optional services don't fail health
  }
}

// GET /health/ready (for k8s readiness probe)
// GET /health/live (for k8s liveness probe)
```

### 8. Better Error Messages & Diagnostics

**Current State:**
- Generic errors when LLM provider misconfigured
- Frontend proxy errors can be cryptic

**Improvements:**

**Startup Diagnostics:**
```
[INFO] Starting Research Agent Network...
[CHECK] Validating configuration...
[✓] AI Provider: Ollama
[CHECK] Testing Ollama connection at http://localhost:11434...
[✗] ERROR: Could not connect to Ollama

    Troubleshooting:
    1. Install Ollama: https://ollama.com/download
    2. Start Ollama: ollama serve
    3. Test connection: curl http://localhost:11434/api/tags
    4. Update appsettings.json endpoint if using non-default port

[SKIP] Starting application without LLM provider (will fail on first task)
```

**Runtime Diagnostics Endpoint:**
```bash
# GET /api/diagnostics
{
  "version": "1.0.0",
  "uptime": "2h 15m",
  "tasks": {
    "total": 42,
    "pending": 3,
    "running": 2,
    "completed": 37
  },
  "llm": {
    "provider": "Ollama",
    "model": "llama3.1:latest",
    "status": "connected",
    "lastRequest": "2025-01-07T10:30:00Z"
  }
}
```

### 9. Database Management Improvements

**Current State:**
- Migrations auto-apply on startup (good)
- Can't easily rollback or view migration status

**Improvements:**

**Migration CLI:**
```bash
ran db migrate              # Apply pending migrations
ran db rollback             # Rollback last migration
ran db status               # Show migration status
ran db seed                 # Seed sample data
ran db reset                # Drop + recreate (dev only)
```

**Migration Notifications:**
```
[INFO] Database: 3 pending migrations detected
[INFO] Applying migration: 20250107_AddReportTable...
[✓] Migration applied successfully
[INFO] Database is up to date
```

### 10. Monitoring & Observability

**Current State:**
- Console logging only
- No metrics collection
- Limited visibility into agent performance

**Improvements:**

**Structured Logging:**
```csharp
// Use Serilog with structured output
logger.Information("Task {TaskId} completed in {Duration}ms with confidence {Confidence}",
    taskId, duration, confidence);

// Output to: Console, File, Seq, Application Insights
```

**Metrics Dashboard:**
```bash
# Expose Prometheus metrics
GET /metrics

# Example metrics:
# - ran_tasks_total{status="completed"}
# - ran_task_duration_seconds{agent="executor"}
# - ran_llm_requests_total{provider="ollama"}
```

**OpenTelemetry Integration:**
- Trace requests across agents
- Visualize task decomposition tree
- Monitor LLM latency and token usage

### Summary of Quick Wins

1. **Docker Compose setup** - Easy full-stack startup
2. **Unified `run.sh` script** - Single entry point for all commands
3. **Configuration validator** - Catch issues before runtime
4. **Health check endpoint** - `/health` for monitoring
5. **Consolidated frontend** - Pick one UI as primary
6. **User Secrets for API keys** - Stop committing secrets
7. **`dotnet watch` by default** - Hot reload for backend
8. **Better error messages** - Actionable troubleshooting steps

### Implementation Priority

**Phase 1 (Immediate):**
- Add `run.sh`/`run.ps1` script for unified startup
- Move API keys to User Secrets
- Add configuration validation on startup
- Improve error messages for common issues

**Phase 2 (Short-term):**
- Create Docker Compose setup
- Add health check endpoints
- Consolidate to single frontend
- Add `ran doctor` dependency checker

**Phase 3 (Long-term):**
- Full CI/CD pipeline
- OpenTelemetry integration
- Kubernetes deployment manifests
- Interactive setup wizard

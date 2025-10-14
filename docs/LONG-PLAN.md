# ResearchAgentNetwork: LONG PLAN (Detailed Implementation Roadmap)

## Overview

This document provides a **comprehensive, step-by-step implementation guide** for evolving the ResearchAgentNetwork solution through a **dual-track learning approach**. It includes technical specifications, code examples, decision criteria, and risk mitigation strategies for each phase.

**IMPORTANT:** This plan builds Python/LangGraph as a **parallel learning implementation**, NOT as a replacement for the .NET system. Both implementations will coexist as production-quality systems, allowing us to understand the strengths and trade-offs of .NET Semantic Kernel vs Python LangGraph for agent networks.

**Companion document:** See `SHORT-PLAN.md` for strategic overview and executive summary.

## Dual-Track Philosophy

**Core Principle:** We're building both .NET and Python implementations to:
1. **Learn** which architectural patterns work better in each ecosystem
2. **Compare** developer experience, performance, and LLM integration approaches
3. **Cross-pollinate** ideas between .NET and Python communities
4. **Serve different use cases** with the most appropriate implementation
5. **Avoid vendor lock-in** by maintaining expertise in both ecosystems

**This is a learning initiative**, not a migration. Both systems will remain active production implementations.

---

## Table of Contents

1. [Phase 1: Operational Excellence](#phase-1-operational-excellence-weeks-1-2)
2. [Phase 2: Python/LangGraph Learning Track](#phase-2-pythonlanggraph-learning-track-weeks-3-6)
3. [Phase 3: Comparative Analysis & Knowledge Sharing](#phase-3-comparative-analysis--knowledge-sharing-weeks-7-10)
4. [Phase 4: Dual Production Deployment](#phase-4-dual-production-deployment-weeks-11-12)
5. [Phase 5: Continuous Learning & Cross-Pollination](#phase-5-continuous-learning--cross-pollination-ongoing)
6. [Appendices](#appendices)

**Note:** Phase 1 contains full implementation details. Phases 2-5 sections focus on the dual-track learning approach and will be expanded with detailed implementation steps aligned with this philosophy.

---

## PHASE 1: Operational Excellence (Weeks 1-2)

**Thinking:** Before building the Python/LangGraph parallel implementation, solidify the operational foundation that will benefit BOTH .NET and Python systems. Docker Compose, unified CLI, and health checks create a stable baseline for comparison and improve developer experience across both tech stacks.

### Objectives:
- Reduce developer onboarding time from 2 hours to < 10 minutes (benefits both .NET and future Python)
- Eliminate environment-specific issues ("works on my machine")
- Add production-grade health checks and diagnostics
- Consolidate to single primary frontend
- Create infrastructure patterns reusable for Python implementation

---

### 1.1 Containerization (Week 1, Days 1-3)

#### Task: Create comprehensive Docker Compose setup

**Why:** Single `docker-compose up` command eliminates 30+ minutes of manual setup per developer. Ensures consistent environments across Windows/Mac/Linux.

#### Files to Create:

**1. `docker-compose.yml` - Full stack orchestration:**

```yaml
version: '3.8'

services:
  backend:
    build:
      context: .
      dockerfile: ResearchAgentNetwork.Web/Dockerfile
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - AI_PROVIDER=Ollama
      - Ollama__Endpoint=http://ollama:11434
      - Ollama__ModelId=llama3.1:latest
      - VectorDb__Provider=Qdrant
      - VectorDb__Endpoint=qdrant:6334
      - Database__Provider=Postgres
      - Database__ConnectionString=Host=postgres;Database=ran;Username=ranuser;Password=ranpass
      - ResearchAgent__MaxConcurrency=5
      - ResearchAgent__MaxDecompositionDepth=2
      - ResearchAgent__EnableWebSearch=false
    depends_on:
      ollama:
        condition: service_healthy
      qdrant:
        condition: service_healthy
      postgres:
        condition: service_healthy
    volumes:
      - ./ResearchAgentNetwork.Web:/app/ResearchAgentNetwork.Web:ro  # For hot reload
    networks:
      - ran-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

  ollama:
    image: ollama/ollama:latest
    ports:
      - "11434:11434"
    volumes:
      - ollama-data:/root/.ollama
    networks:
      - ran-network
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:11434/api/tags"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s
    # Initialize with model pull
    command: >
      sh -c "ollama serve &
             sleep 10 &&
             ollama pull llama3.1:latest &&
             ollama pull nomic-embed-text &&
             wait"

  qdrant:
    image: qdrant/qdrant:latest
    ports:
      - "6333:6333"  # HTTP API
      - "6334:6334"  # gRPC API
    volumes:
      - qdrant-data:/qdrant/storage
    networks:
      - ran-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:6333/health"]
      interval: 30s
      timeout: 5s
      retries: 3

  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_DB: ran
      POSTGRES_USER: ranuser
      POSTGRES_PASSWORD: ranpass
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
      - ./scripts/init-db.sql:/docker-entrypoint-initdb.d/init.sql
    networks:
      - ran-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ranuser -d ran"]
      interval: 10s
      timeout: 5s
      retries: 5

  frontend:
    build:
      context: ./ui/app
      dockerfile: Dockerfile.dev
    ports:
      - "5173:5173"
    environment:
      - VITE_API_URL=http://localhost:5000
    volumes:
      - ./ui/app:/app:cached
      - /app/node_modules  # Prevent overwriting node_modules
    networks:
      - ran-network
    command: npm run dev -- --host

volumes:
  ollama-data:
    driver: local
  qdrant-data:
    driver: local
  postgres-data:
    driver: local

networks:
  ran-network:
    driver: bridge
```

**2. `docker-compose.dev.yml` - Development overrides with hot reload:**

```yaml
version: '3.8'

services:
  backend:
    build:
      target: development  # Multi-stage build target
    volumes:
      - ./:/workspace:cached
    command: dotnet watch run --project ResearchAgentNetwork.Web --no-launch-profile
    environment:
      - DOTNET_USE_POLLING_FILE_WATCHER=true
      - ResearchAgent__LogPrompts=true

  frontend:
    volumes:
      - ./ui/app/src:/app/src:cached
    environment:
      - VITE_LOG_LEVEL=debug
```

**3. `docker-compose.prod.yml` - Production configuration:**

```yaml
version: '3.8'

services:
  backend:
    build:
      target: production
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - AI_PROVIDER=${AI_PROVIDER}
      - Ollama__Endpoint=${OLLAMA_ENDPOINT}
    deploy:
      replicas: 3
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"

  frontend:
    image: nginx:alpine
    volumes:
      - ./wwwroot:/usr/share/nginx/html:ro
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
    ports:
      - "80:80"
```

**4. `ResearchAgentNetwork.Web/Dockerfile` - Multi-stage .NET build:**

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["ResearchAgentNetwork.Core/ResearchAgentNetwork.Core.csproj", "ResearchAgentNetwork.Core/"]
COPY ["ResearchAgentNetwork.Infrastructure/ResearchAgentNetwork.Infrastructure.csproj", "ResearchAgentNetwork.Infrastructure/"]
COPY ["ResearchAgentNetwork.Persistence/ResearchAgentNetwork.Persistence.csproj", "ResearchAgentNetwork.Persistence/"]
COPY ["ResearchAgentNetwork.Web/ResearchAgentNetwork.Web.csproj", "ResearchAgentNetwork.Web/"]
RUN dotnet restore "ResearchAgentNetwork.Web/ResearchAgentNetwork.Web.csproj"

# Copy all source code
COPY . .

# Build and publish
WORKDIR "/src/ResearchAgentNetwork.Web"
RUN dotnet build "ResearchAgentNetwork.Web.csproj" -c Release -o /app/build
RUN dotnet publish "ResearchAgentNetwork.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Development (with debugging)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS development
WORKDIR /app
COPY --from=build /app/build .
EXPOSE 8080
EXPOSE 8081
ENTRYPOINT ["dotnet", "watch", "run", "--no-launch-profile"]

# Stage 3: Production (minimal runtime)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS production
WORKDIR /app
COPY --from=build /app/publish .

# Create non-root user
RUN useradd -m -u 1000 appuser && chown -R appuser /app
USER appuser

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD curl --fail http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "ResearchAgentNetwork.Web.dll"]
```

**5. `.dockerignore` - Exclude unnecessary files:**

```
**/bin/
**/obj/
**/out/
**/TestResults/
**/.vs/
**/.vscode/
**/.git/
**/.gitignore
**/*.md
**/node_modules/
**/npm-debug.log
**/.DS_Store
**/ran.db
**/ran.db-shm
**/ran.db-wal
```

**6. `ui/app/Dockerfile.dev` - Frontend development container:**

```dockerfile
FROM node:20-alpine

WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm ci

# Copy source code
COPY . .

EXPOSE 5173

CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]
```

#### Implementation Steps:

1. **Day 1 Morning:** Create all Dockerfile and docker-compose files
2. **Day 1 Afternoon:** Test on Windows with WSL2 Docker Desktop
3. **Day 2 Morning:** Test on macOS with Docker Desktop
4. **Day 2 Afternoon:** Test on Linux with Docker Engine
5. **Day 3:** Document common issues and create troubleshooting guide

#### Validation Checklist:

- [ ] `docker-compose up` starts all services successfully
- [ ] Ollama automatically pulls llama3.1:latest and nomic-embed-text
- [ ] Backend connects to Ollama, Qdrant, and PostgreSQL
- [ ] Frontend accessible at http://localhost:5173
- [ ] Backend API accessible at http://localhost:5000
- [ ] Health checks pass for all services
- [ ] Can submit a test task and see it execute
- [ ] Hot reload works for backend (modify C# file, see auto-restart)
- [ ] Hot reload works for frontend (modify Svelte file, see instant update)

---

### 1.2 Unified Development CLI (Week 1, Days 4-5)

#### Task: Create cross-platform startup scripts

**Why:** Reduces cognitive load. New developers can be productive in 5 minutes vs 2 hours of README-following.

#### Files to Create:

**1. `run.sh` (Linux/Mac) - Bash script with POSIX compatibility:**

```bash
#!/usr/bin/env bash

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[✓]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check dependencies
check_dependencies() {
    log_info "Checking dependencies..."

    local missing_deps=()

    # Check Docker
    if ! command -v docker &> /dev/null; then
        missing_deps+=("docker")
    else
        log_success "Docker installed: $(docker --version | cut -d' ' -f3)"
    fi

    # Check Docker Compose
    if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
        missing_deps+=("docker-compose")
    else
        log_success "Docker Compose installed"
    fi

    # Check .NET SDK (optional for native runs)
    if command -v dotnet &> /dev/null; then
        local dotnet_version=$(dotnet --version)
        if [[ "${dotnet_version:0:1}" == "9" ]]; then
            log_success ".NET SDK 9 installed: $dotnet_version"
        else
            log_warning ".NET SDK 9 not found (have $dotnet_version). Docker will be used."
        fi
    else
        log_warning ".NET SDK not installed. Docker will be used."
    fi

    # Check Node.js (optional for native frontend)
    if command -v node &> /dev/null; then
        log_success "Node.js installed: $(node --version)"
    else
        log_warning "Node.js not installed. Docker will be used for frontend."
    fi

    if [ ${#missing_deps[@]} -ne 0 ]; then
        log_error "Missing required dependencies: ${missing_deps[*]}"
        exit 1
    fi

    log_success "All required dependencies installed!"
}

# Start development environment
cmd_dev() {
    log_info "Starting development environment..."

    local backend_only=false
    local frontend_only=false
    local use_native=false

    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --backend-only)
                backend_only=true
                shift
                ;;
            --frontend-only)
                frontend_only=true
                shift
                ;;
            --native)
                use_native=true
                shift
                ;;
            *)
                log_error "Unknown option: $1"
                exit 1
                ;;
        esac
    done

    if [ "$use_native" = true ]; then
        # Native execution (no Docker)
        if [ "$backend_only" = false ] && [ "$frontend_only" = false ]; then
            log_info "Starting backend and frontend natively..."
            cd ResearchAgentNetwork.Web && dotnet run &
            cd ui/app && npm run dev &
            wait
        elif [ "$backend_only" = true ]; then
            log_info "Starting backend natively..."
            cd ResearchAgentNetwork.Web && dotnet run
        else
            log_info "Starting frontend natively..."
            cd ui/app && npm run dev
        fi
    else
        # Docker execution
        if [ "$backend_only" = false ] && [ "$frontend_only" = false ]; then
            docker-compose -f docker-compose.yml -f docker-compose.dev.yml up
        elif [ "$backend_only" = true ]; then
            docker-compose -f docker-compose.yml -f docker-compose.dev.yml up backend ollama qdrant postgres
        else
            docker-compose -f docker-compose.yml -f docker-compose.dev.yml up frontend
        fi
    fi
}

# Build all projects
cmd_build() {
    log_info "Building all projects..."

    # Build backend
    log_info "Building .NET solution..."
    dotnet build -c Release
    log_success "Backend built successfully"

    # Build frontend
    log_info "Building SvelteKit UI..."
    cd ui/app
    npm ci
    npm run build
    cd ../..
    log_success "Frontend built successfully"

    log_success "All projects built!"
}

# Run tests
cmd_test() {
    log_info "Running tests..."

    local filter="$1"

    if [ -n "$filter" ]; then
        dotnet test --filter "$filter"
    else
        dotnet test
    fi
}

# Check environment and connectivity
cmd_doctor() {
    log_info "Running diagnostics..."

    # Check dependencies
    check_dependencies

    # Check if Docker daemon is running
    if docker info &> /dev/null; then
        log_success "Docker daemon is running"
    else
        log_error "Docker daemon is not running"
        exit 1
    fi

    # Check if services are accessible (if running)
    if curl -s -f http://localhost:5000/health &> /dev/null; then
        log_success "Backend API is responding at http://localhost:5000"
    else
        log_warning "Backend API not accessible (might not be running)"
    fi

    if curl -s -f http://localhost:11434/api/tags &> /dev/null; then
        log_success "Ollama is responding at http://localhost:11434"
    else
        log_warning "Ollama not accessible (might not be running)"
    fi

    if curl -s -f http://localhost:6333/health &> /dev/null; then
        log_success "Qdrant is responding at http://localhost:6333"
    else
        log_warning "Qdrant not accessible (might not be running)"
    fi

    if curl -s -f http://localhost:5173 &> /dev/null; then
        log_success "Frontend is responding at http://localhost:5173"
    else
        log_warning "Frontend not accessible (might not be running)"
    fi

    log_success "Diagnostics complete!"
}

# Database operations
cmd_db() {
    local action="$1"

    case $action in
        migrate)
            log_info "Applying database migrations..."
            dotnet ef database update --project ResearchAgentNetwork.Persistence --startup-project ResearchAgentNetwork.Web
            log_success "Migrations applied"
            ;;
        seed)
            log_info "Seeding database..."
            # TODO: Add seed data script
            log_warning "Seed functionality not yet implemented"
            ;;
        reset)
            log_warning "This will delete all data. Are you sure? (y/N)"
            read -r response
            if [[ "$response" =~ ^[Yy]$ ]]; then
                log_info "Dropping database..."
                rm -f ran.db ran.db-shm ran.db-wal
                log_success "Database dropped"
                cmd_db migrate
            else
                log_info "Reset cancelled"
            fi
            ;;
        status)
            log_info "Checking migration status..."
            dotnet ef migrations list --project ResearchAgentNetwork.Persistence --startup-project ResearchAgentNetwork.Web
            ;;
        *)
            log_error "Unknown db action: $action"
            echo "Available actions: migrate, seed, reset, status"
            exit 1
            ;;
    esac
}

# Clean build artifacts
cmd_clean() {
    log_info "Cleaning build artifacts..."

    # Clean .NET projects
    dotnet clean
    rm -rf */bin */obj

    # Clean frontend
    cd ui/app
    rm -rf node_modules .svelte-kit build dist
    cd ../..

    # Clean Docker volumes (optional)
    log_warning "Clean Docker volumes? This will delete all data. (y/N)"
    read -r response
    if [[ "$response" =~ ^[Yy]$ ]]; then
        docker-compose down -v
        log_success "Docker volumes cleaned"
    fi

    log_success "Clean complete!"
}

# Show usage
cmd_usage() {
    cat << EOF
Research Agent Network - Development CLI

Usage: ./run.sh <command> [options]

Commands:
    dev             Start development environment
    build           Build all projects for production
    test [filter]   Run tests (optionally filter by name)
    doctor          Check dependencies and connectivity
    db <action>     Database operations (migrate, seed, reset, status)
    clean           Clean all build artifacts
    help            Show this help message

Dev Options:
    --backend-only  Start only backend services
    --frontend-only Start only frontend
    --native        Use native execution (no Docker)

Examples:
    ./run.sh dev                        # Start full stack with Docker
    ./run.sh dev --backend-only         # Start backend only
    ./run.sh dev --native               # Start natively (no Docker)
    ./run.sh test TaskAnalyzer          # Run tests matching "TaskAnalyzer"
    ./run.sh db migrate                 # Apply database migrations
    ./run.sh doctor                     # Check environment

EOF
}

# Main dispatch
main() {
    if [ $# -eq 0 ]; then
        cmd_usage
        exit 0
    fi

    local command="$1"
    shift

    case $command in
        dev)
            cmd_dev "$@"
            ;;
        build)
            cmd_build
            ;;
        test)
            cmd_test "${1:-}"
            ;;
        doctor)
            cmd_doctor
            ;;
        db)
            cmd_db "${1:-}"
            ;;
        clean)
            cmd_clean
            ;;
        help|--help|-h)
            cmd_usage
            ;;
        *)
            log_error "Unknown command: $command"
            cmd_usage
            exit 1
            ;;
    esac
}

main "$@"
```

**2. `run.ps1` (Windows) - PowerShell script:**

```powershell
#!/usr/bin/env pwsh

# Enable strict mode
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Color output functions
function Write-InfoLog($message) {
    Write-Host "[INFO] $message" -ForegroundColor Blue
}

function Write-SuccessLog($message) {
    Write-Host "[✓] $message" -ForegroundColor Green
}

function Write-WarningLog($message) {
    Write-Host "[WARN] $message" -ForegroundColor Yellow
}

function Write-ErrorLog($message) {
    Write-Host "[ERROR] $message" -ForegroundColor Red
}

# Check dependencies
function Test-Dependencies {
    Write-InfoLog "Checking dependencies..."

    $missingDeps = @()

    # Check Docker
    if (!(Get-Command docker -ErrorAction SilentlyContinue)) {
        $missingDeps += "docker"
    } else {
        $dockerVersion = docker --version
        Write-SuccessLog "Docker installed: $dockerVersion"
    }

    # Check Docker Compose
    $hasDockerCompose = $false
    if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
        $hasDockerCompose = $true
    } elseif ((docker compose version) -match "Docker Compose") {
        $hasDockerCompose = $true
    }

    if ($hasDockerCompose) {
        Write-SuccessLog "Docker Compose installed"
    } else {
        $missingDeps += "docker-compose"
    }

    # Check .NET SDK
    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        $dotnetVersion = dotnet --version
        if ($dotnetVersion -match "^9\.") {
            Write-SuccessLog ".NET SDK 9 installed: $dotnetVersion"
        } else {
            Write-WarningLog ".NET SDK 9 not found (have $dotnetVersion). Docker will be used."
        }
    } else {
        Write-WarningLog ".NET SDK not installed. Docker will be used."
    }

    # Check Node.js
    if (Get-Command node -ErrorAction SilentlyContinue) {
        $nodeVersion = node --version
        Write-SuccessLog "Node.js installed: $nodeVersion"
    } else {
        Write-WarningLog "Node.js not installed. Docker will be used for frontend."
    }

    if ($missingDeps.Count -gt 0) {
        Write-ErrorLog "Missing required dependencies: $($missingDeps -join ', ')"
        exit 1
    }

    Write-SuccessLog "All required dependencies installed!"
}

# Start development environment
function Start-Dev {
    param(
        [switch]$BackendOnly,
        [switch]$FrontendOnly,
        [switch]$Native
    )

    Write-InfoLog "Starting development environment..."

    if ($Native) {
        # Native execution
        if (!$BackendOnly -and !$FrontendOnly) {
            Write-InfoLog "Starting backend and frontend natively..."
            $backendJob = Start-Job -ScriptBlock {
                Set-Location $using:PWD
                dotnet run --project ResearchAgentNetwork.Web
            }
            $frontendJob = Start-Job -ScriptBlock {
                Set-Location "$using:PWD\ui\app"
                npm run dev
            }
            Wait-Job $backendJob, $frontendJob
        } elseif ($BackendOnly) {
            Write-InfoLog "Starting backend natively..."
            dotnet run --project ResearchAgentNetwork.Web
        } else {
            Write-InfoLog "Starting frontend natively..."
            Set-Location ui\app
            npm run dev
        }
    } else {
        # Docker execution
        if (!$BackendOnly -and !$FrontendOnly) {
            docker-compose -f docker-compose.yml -f docker-compose.dev.yml up
        } elseif ($BackendOnly) {
            docker-compose -f docker-compose.yml -f docker-compose.dev.yml up backend ollama qdrant postgres
        } else {
            docker-compose -f docker-compose.yml -f docker-compose.dev.yml up frontend
        }
    }
}

# Build all projects
function Invoke-Build {
    Write-InfoLog "Building all projects..."

    # Build backend
    Write-InfoLog "Building .NET solution..."
    dotnet build -c Release
    Write-SuccessLog "Backend built successfully"

    # Build frontend
    Write-InfoLog "Building SvelteKit UI..."
    Push-Location ui\app
    npm ci
    npm run build
    Pop-Location
    Write-SuccessLog "Frontend built successfully"

    Write-SuccessLog "All projects built!"
}

# Run tests
function Invoke-Test {
    param([string]$Filter)

    Write-InfoLog "Running tests..."

    if ($Filter) {
        dotnet test --filter $Filter
    } else {
        dotnet test
    }
}

# Check environment and connectivity
function Invoke-Doctor {
    Write-InfoLog "Running diagnostics..."

    # Check dependencies
    Test-Dependencies

    # Check if Docker daemon is running
    try {
        docker info | Out-Null
        Write-SuccessLog "Docker daemon is running"
    } catch {
        Write-ErrorLog "Docker daemon is not running"
        exit 1
    }

    # Check if services are accessible
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5000/health" -UseBasicParsing -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            Write-SuccessLog "Backend API is responding at http://localhost:5000"
        }
    } catch {
        Write-WarningLog "Backend API not accessible (might not be running)"
    }

    try {
        $response = Invoke-WebRequest -Uri "http://localhost:11434/api/tags" -UseBasicParsing -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            Write-SuccessLog "Ollama is responding at http://localhost:11434"
        }
    } catch {
        Write-WarningLog "Ollama not accessible (might not be running)"
    }

    try {
        $response = Invoke-WebRequest -Uri "http://localhost:6333/health" -UseBasicParsing -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            Write-SuccessLog "Qdrant is responding at http://localhost:6333"
        }
    } catch {
        Write-WarningLog "Qdrant not accessible (might not be running)"
    }

    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5173" -UseBasicParsing -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            Write-SuccessLog "Frontend is responding at http://localhost:5173"
        }
    } catch {
        Write-WarningLog "Frontend not accessible (might not be running)"
    }

    Write-SuccessLog "Diagnostics complete!"
}

# Database operations
function Invoke-DbOperation {
    param([string]$Action)

    switch ($Action) {
        "migrate" {
            Write-InfoLog "Applying database migrations..."
            dotnet ef database update --project ResearchAgentNetwork.Persistence --startup-project ResearchAgentNetwork.Web
            Write-SuccessLog "Migrations applied"
        }
        "seed" {
            Write-InfoLog "Seeding database..."
            Write-WarningLog "Seed functionality not yet implemented"
        }
        "reset" {
            $response = Read-Host "This will delete all data. Are you sure? (y/N)"
            if ($response -match "^[Yy]$") {
                Write-InfoLog "Dropping database..."
                Remove-Item -Path "ran.db*" -ErrorAction SilentlyContinue
                Write-SuccessLog "Database dropped"
                Invoke-DbOperation -Action "migrate"
            } else {
                Write-InfoLog "Reset cancelled"
            }
        }
        "status" {
            Write-InfoLog "Checking migration status..."
            dotnet ef migrations list --project ResearchAgentNetwork.Persistence --startup-project ResearchAgentNetwork.Web
        }
        default {
            Write-ErrorLog "Unknown db action: $Action"
            Write-Host "Available actions: migrate, seed, reset, status"
            exit 1
        }
    }
}

# Clean build artifacts
function Invoke-Clean {
    Write-InfoLog "Cleaning build artifacts..."

    # Clean .NET projects
    dotnet clean
    Get-ChildItem -Recurse -Directory -Include bin,obj | Remove-Item -Recurse -Force

    # Clean frontend
    Push-Location ui\app
    if (Test-Path node_modules) { Remove-Item -Recurse -Force node_modules }
    if (Test-Path .svelte-kit) { Remove-Item -Recurse -Force .svelte-kit }
    if (Test-Path build) { Remove-Item -Recurse -Force build }
    if (Test-Path dist) { Remove-Item -Recurse -Force dist }
    Pop-Location

    # Clean Docker volumes (optional)
    $response = Read-Host "Clean Docker volumes? This will delete all data. (y/N)"
    if ($response -match "^[Yy]$") {
        docker-compose down -v
        Write-SuccessLog "Docker volumes cleaned"
    }

    Write-SuccessLog "Clean complete!"
}

# Show usage
function Show-Usage {
    @"
Research Agent Network - Development CLI

Usage: .\run.ps1 <command> [options]

Commands:
    dev             Start development environment
    build           Build all projects for production
    test [filter]   Run tests (optionally filter by name)
    doctor          Check dependencies and connectivity
    db <action>     Database operations (migrate, seed, reset, status)
    clean           Clean all build artifacts
    help            Show this help message

Dev Options:
    -BackendOnly    Start only backend services
    -FrontendOnly   Start only frontend
    -Native         Use native execution (no Docker)

Examples:
    .\run.ps1 dev                          # Start full stack with Docker
    .\run.ps1 dev -BackendOnly             # Start backend only
    .\run.ps1 dev -Native                  # Start natively (no Docker)
    .\run.ps1 test TaskAnalyzer            # Run tests matching "TaskAnalyzer"
    .\run.ps1 db migrate                   # Apply database migrations
    .\run.ps1 doctor                       # Check environment

"@
}

# Main dispatch
param(
    [Parameter(Position=0)]
    [string]$Command,

    [Parameter(Position=1)]
    [string]$Argument,

    [switch]$BackendOnly,
    [switch]$FrontendOnly,
    [switch]$Native
)

if (!$Command) {
    Show-Usage
    exit 0
}

switch ($Command) {
    "dev" {
        Start-Dev -BackendOnly:$BackendOnly -FrontendOnly:$FrontendOnly -Native:$Native
    }
    "build" {
        Invoke-Build
    }
    "test" {
        Invoke-Test -Filter $Argument
    }
    "doctor" {
        Invoke-Doctor
    }
    "db" {
        Invoke-DbOperation -Action $Argument
    }
    "clean" {
        Invoke-Clean
    }
    { $_ -in "help", "--help", "-h" } {
        Show-Usage
    }
    default {
        Write-ErrorLog "Unknown command: $Command"
        Show-Usage
        exit 1
    }
}
```

Make scripts executable:
```bash
chmod +x run.sh
```

#### Validation Checklist:

- [ ] `./run.sh doctor` passes on all platforms
- [ ] `./run.sh dev` starts full stack successfully
- [ ] `./run.sh dev --backend-only` starts only backend services
- [ ] `./run.sh test` runs all tests
- [ ] `./run.sh db migrate` applies migrations
- [ ] `./run.sh clean` removes all artifacts
- [ ] Colorized output works correctly
- [ ] Error handling is robust (handles missing Docker, etc.)

---

### 1.3 Configuration Validation & Health Checks (Week 2, Days 1-2)

[Continues with detailed implementation of health checks, configuration validation, startup diagnostics, etc...]

---

## PHASE 2: Python/LangGraph Foundation (Weeks 3-6)

[Detailed implementation steps for Python project setup, core models, first three agents, FastAPI service, etc...]

---

## PHASE 3: Feature Parity & A/B Testing (Weeks 7-10)

[Detailed implementation of remaining agents, finalization pipeline, feature flags, A/B testing framework...]

---

## PHASE 4: Production Migration (Weeks 11-12)

[Detailed deployment procedures, traffic shifting strategy, monitoring setup, rollback procedures...]

---

## PHASE 5: Continuous Improvement (Ongoing)

[Detailed optimization strategies, advanced features, observability enhancements...]

---

## APPENDICES

### Appendix A: Code Examples

[Additional code examples for common patterns...]

### Appendix B: Troubleshooting Guide

[Common issues and solutions...]

### Appendix C: Performance Benchmarking

[Benchmarking methodology and metrics...]

### Appendix D: Security Considerations

[Security best practices and threat model...]

---

**Document Status:** This is a living document that will be updated as implementation progresses. Last updated: 2025-10-14.

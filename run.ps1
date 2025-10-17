#!/usr/bin/env pwsh

# ResearchAgentNetwork Unified CLI Script (PowerShell)
# Provides cross-platform commands for development, testing, and deployment

param(
    [Parameter(Position = 0)]
    [string]$Command = "help",

    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

$ErrorActionPreference = "Stop"

# Color functions
function Write-Header {
    param([string]$Message)
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  $Message" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠ $Message" -ForegroundColor Yellow
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ $Message" -ForegroundColor Blue
}

# Check if command exists
function Test-CommandExists {
    param([string]$Command)
    $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

# Show usage information
function Show-Usage {
    Write-Host "ResearchAgentNetwork CLI" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor Green
    Write-Host "  .\run.ps1 <command> [options]"
    Write-Host ""

    Write-Host "Commands:" -ForegroundColor Green
    Write-Host "  dev              " -ForegroundColor Blue -NoNewline
    Write-Host "Start development environment (Docker with hot reload)"
    Write-Host "  prod             " -ForegroundColor Blue -NoNewline
    Write-Host "Start production environment (Docker optimized)"
    Write-Host "  stop             " -ForegroundColor Blue -NoNewline
    Write-Host "Stop all running containers"
    Write-Host "  down             " -ForegroundColor Blue -NoNewline
    Write-Host "Stop and remove all containers and networks"
    Write-Host "  build            " -ForegroundColor Blue -NoNewline
    Write-Host "Build .NET solution"
    Write-Host "  test             " -ForegroundColor Blue -NoNewline
    Write-Host "Run all tests"
    Write-Host "  test-watch       " -ForegroundColor Blue -NoNewline
    Write-Host "Run tests in watch mode"
    Write-Host "  doctor           " -ForegroundColor Blue -NoNewline
    Write-Host "Check dependencies and system health"
    Write-Host "  db migrate       " -ForegroundColor Blue -NoNewline
    Write-Host "Apply database migrations"
    Write-Host "  db seed          " -ForegroundColor Blue -NoNewline
    Write-Host "Seed database with sample data"
    Write-Host "  db reset         " -ForegroundColor Blue -NoNewline
    Write-Host "Reset database (drop and recreate)"
    Write-Host "  db status        " -ForegroundColor Blue -NoNewline
    Write-Host "Show database migration status"
    Write-Host "  clean            " -ForegroundColor Blue -NoNewline
    Write-Host "Remove build artifacts and caches"
    Write-Host "  logs             " -ForegroundColor Blue -NoNewline
    Write-Host "Show Docker logs (optional: service name)"
    Write-Host "  ps               " -ForegroundColor Blue -NoNewline
    Write-Host "Show running containers"
    Write-Host "  help             " -ForegroundColor Blue -NoNewline
    Write-Host "Show this help message"
    Write-Host ""

    Write-Host "Examples:" -ForegroundColor Green
    Write-Host "  .\run.ps1 dev                    # Start development environment"
    Write-Host "  .\run.ps1 test                   # Run tests"
    Write-Host "  .\run.ps1 db migrate             # Apply migrations"
    Write-Host "  .\run.ps1 logs backend           # Show backend logs"
    Write-Host "  .\run.ps1 doctor                 # Check system health"
    Write-Host ""

    Write-Host "Development Workflow:" -ForegroundColor Green
    Write-Host "  1. .\run.ps1 doctor              # Check prerequisites"
    Write-Host "  2. .\run.ps1 dev                 # Start services"
    Write-Host "  3. .\run.ps1 logs                # Monitor logs"
    Write-Host "  4. .\run.ps1 stop                # Stop when done"
    Write-Host ""
}

# Doctor command - check dependencies
function Invoke-Doctor {
    Write-Header "System Health Check"

    $allGood = $true

    # Check .NET SDK
    if (Test-CommandExists "dotnet") {
        $dotnetVersion = (dotnet --version)
        Write-Success ".NET SDK: $dotnetVersion"
        if (-not $dotnetVersion.StartsWith("9.")) {
            Write-Warning ".NET 9 recommended (found: $dotnetVersion)"
        }
    }
    else {
        Write-ErrorMsg ".NET SDK not found"
        $allGood = $false
    }

    # Check Docker
    if (Test-CommandExists "docker") {
        $dockerVersion = (docker --version) -replace "Docker version ", "" -replace ",.*", ""
        Write-Success "Docker: $dockerVersion"

        try {
            docker ps | Out-Null
            Write-Success "Docker daemon is running"
        }
        catch {
            Write-ErrorMsg "Docker daemon is not running"
            Write-Info "Please start Docker Desktop"
            $allGood = $false
        }
    }
    else {
        Write-ErrorMsg "Docker not found"
        $allGood = $false
    }

    # Check Docker Compose
    if (Test-CommandExists "docker") {
        try {
            $composeVersion = (docker compose version --short)
            Write-Success "Docker Compose: $composeVersion"
        }
        catch {
            Write-ErrorMsg "Docker Compose not found"
            $allGood = $false
        }
    }

    # Check Node.js
    if (Test-CommandExists "node") {
        $nodeVersion = (node --version)
        Write-Success "Node.js: $nodeVersion"
    }
    else {
        Write-Warning "Node.js not found (required for frontend development)"
    }

    # Check npm
    if (Test-CommandExists "npm") {
        $npmVersion = (npm --version)
        Write-Success "npm: $npmVersion"
    }
    else {
        Write-Warning "npm not found (required for frontend development)"
    }

    # Check Git
    if (Test-CommandExists "git") {
        $gitVersion = (git --version) -replace "git version ", ""
        Write-Success "Git: $gitVersion"
    }
    else {
        Write-Warning "Git not found"
    }

    Write-Host ""

    # Check project files
    Write-Info "Checking project structure..."

    if (Test-Path "ResearchAgentNetwork.sln") {
        Write-Success "Solution file found"
    }
    else {
        Write-ErrorMsg "ResearchAgentNetwork.sln not found"
        $allGood = $false
    }

    if (Test-Path "docker-compose.yml") {
        Write-Success "docker-compose.yml found"
    }
    else {
        Write-ErrorMsg "docker-compose.yml not found"
        $allGood = $false
    }

    if (Test-Path "docker-compose.dev.yml") {
        Write-Success "docker-compose.dev.yml found"
    }
    else {
        Write-Warning "docker-compose.dev.yml not found (optional for dev mode)"
    }

    Write-Host ""

    if ($allGood) {
        Write-Success "All critical dependencies satisfied!"
        Write-Info "You're ready to start: .\run.ps1 dev"
    }
    else {
        Write-ErrorMsg "Some dependencies are missing. Please install them first."
        exit 1
    }
}

# Dev command - start development environment
function Invoke-Dev {
    Write-Header "Starting Development Environment"

    Write-Info "Starting services with hot reload enabled..."
    docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
}

# Prod command - start production environment
function Invoke-Prod {
    Write-Header "Starting Production Environment"

    Write-Info "Starting optimized production services..."
    docker compose up --build -d

    Write-Host ""
    Write-Success "Services started!"
    Write-Info "Frontend: http://localhost:5173"
    Write-Info "Backend:  http://localhost:5000"
    Write-Info "Ollama:   http://localhost:11434"
    Write-Info "Qdrant:   http://localhost:6333/dashboard"
    Write-Host ""
    Write-Info "View logs: .\run.ps1 logs"
    Write-Info "Stop:      .\run.ps1 stop"
}

# Stop command
function Invoke-Stop {
    Write-Header "Stopping Services"
    docker compose stop
    Write-Success "All services stopped"
}

# Down command
function Invoke-Down {
    Write-Header "Removing Containers"
    Write-Warning "This will remove containers and networks (volumes preserved)"
    docker compose down
    Write-Success "Cleanup complete"
}

# Build command
function Invoke-Build {
    Write-Header "Building .NET Solution"
    dotnet build
    Write-Success "Build complete"
}

# Test command
function Invoke-Test {
    Write-Header "Running Tests"
    dotnet test --verbosity normal
}

# Test watch command
function Invoke-TestWatch {
    Write-Header "Running Tests (Watch Mode)"
    Write-Info "Press Ctrl+C to stop..."
    dotnet watch test --project ResearchAgentNetwork.Tests
}

# Database migrate command
function Invoke-DbMigrate {
    Write-Header "Applying Database Migrations"

    $backendRunning = docker compose ps backend 2>$null | Select-String "Up"

    if ($backendRunning) {
        Write-Info "Running migrations in container..."
        docker compose exec backend dotnet ef database update --project /app/ResearchAgentNetwork.Persistence --startup-project /app/ResearchAgentNetwork.Web
    }
    else {
        Write-Info "Running migrations locally..."
        dotnet ef database update --project ResearchAgentNetwork.Persistence --startup-project ResearchAgentNetwork.Web
    }

    Write-Success "Migrations applied"
}

# Database seed command
function Invoke-DbSeed {
    Write-Header "Seeding Database"
    Write-Warning "Database seeding not yet implemented"
    Write-Info "You can submit tasks via API: POST http://localhost:5000/api/tasks"
}

# Database reset command
function Invoke-DbReset {
    Write-Header "Resetting Database"
    Write-Warning "This will DELETE all data!"
    $confirmation = Read-Host "Are you sure? (yes/no)"

    if ($confirmation -eq "yes") {
        Write-Info "Removing database files..."
        Remove-Item -Path "ran.db", "ran.db-shm", "ran.db-wal" -ErrorAction SilentlyContinue

        Write-Info "Applying migrations..."
        Invoke-DbMigrate

        Write-Success "Database reset complete"
    }
    else {
        Write-Info "Reset cancelled"
    }
}

# Database status command
function Invoke-DbStatus {
    Write-Header "Database Migration Status"

    $backendRunning = docker compose ps backend 2>$null | Select-String "Up"

    if ($backendRunning) {
        docker compose exec backend dotnet ef migrations list --project /app/ResearchAgentNetwork.Persistence --startup-project /app/ResearchAgentNetwork.Web
    }
    else {
        dotnet ef migrations list --project ResearchAgentNetwork.Persistence --startup-project ResearchAgentNetwork.Web
    }
}

# Clean command
function Invoke-Clean {
    Write-Header "Cleaning Build Artifacts"

    Write-Info "Removing bin/ and obj/ directories..."
    Get-ChildItem -Path . -Include bin, obj -Recurse -Directory | Remove-Item -Recurse -Force

    Write-Info "Removing node_modules/ directories..."
    Get-ChildItem -Path . -Include node_modules -Recurse -Directory | Remove-Item -Recurse -Force

    Write-Info "Removing build caches..."
    Remove-Item -Path "ui\app\.svelte-kit" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "ui-react-router\dist" -Recurse -Force -ErrorAction SilentlyContinue

    Write-Success "Cleanup complete"
}

# Logs command
function Invoke-Logs {
    param([string]$Service)

    if ($Service) {
        Write-Header "Logs: $Service"
        docker compose logs -f $Service
    }
    else {
        Write-Header "Logs: All Services"
        docker compose logs -f
    }
}

# PS command
function Invoke-Ps {
    Write-Header "Running Containers"
    docker compose ps
}

# Main command dispatcher
switch ($Command.ToLower()) {
    "dev" {
        Invoke-Dev
    }
    "prod" {
        Invoke-Prod
    }
    "stop" {
        Invoke-Stop
    }
    "down" {
        Invoke-Down
    }
    "build" {
        Invoke-Build
    }
    "test" {
        Invoke-Test
    }
    "test-watch" {
        Invoke-TestWatch
    }
    "doctor" {
        Invoke-Doctor
    }
    "db" {
        if ($Arguments.Count -eq 0) {
            Write-ErrorMsg "db command requires a subcommand"
            Write-Host ""
            Write-Host "Available db commands: migrate, seed, reset, status"
            exit 1
        }

        $dbCommand = $Arguments[0].ToLower()
        switch ($dbCommand) {
            "migrate" {
                Invoke-DbMigrate
            }
            "seed" {
                Invoke-DbSeed
            }
            "reset" {
                Invoke-DbReset
            }
            "status" {
                Invoke-DbStatus
            }
            default {
                Write-ErrorMsg "Unknown db command: $dbCommand"
                Write-Host ""
                Write-Host "Available db commands: migrate, seed, reset, status"
                exit 1
            }
        }
    }
    "clean" {
        Invoke-Clean
    }
    "logs" {
        $service = if ($Arguments.Count -gt 0) { $Arguments[0] } else { "" }
        Invoke-Logs -Service $service
    }
    "ps" {
        Invoke-Ps
    }
    { $_ -in "help", "--help", "-h" } {
        Show-Usage
    }
    default {
        Write-ErrorMsg "Unknown command: $Command"
        Write-Host ""
        Show-Usage
        exit 1
    }
}

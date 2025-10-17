#!/bin/bash

# ResearchAgentNetwork Unified CLI Script
# Provides cross-platform commands for development, testing, and deployment

set -e  # Exit on error

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Helper functions
print_header() {
    echo -e "${CYAN}═══════════════════════════════════════════════════════${NC}"
    echo -e "${CYAN}  $1${NC}"
    echo -e "${CYAN}═══════════════════════════════════════════════════════${NC}"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ $1${NC}"
}

# Check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Show usage information
show_usage() {
    cat << EOF
${CYAN}ResearchAgentNetwork CLI${NC}

${GREEN}Usage:${NC}
  ./run.sh <command> [options]

${GREEN}Commands:${NC}
  ${BLUE}dev${NC}              Start development environment (Docker with hot reload)
  ${BLUE}prod${NC}             Start production environment (Docker optimized)
  ${BLUE}stop${NC}             Stop all running containers
  ${BLUE}down${NC}             Stop and remove all containers and networks
  ${BLUE}build${NC}            Build .NET solution
  ${BLUE}test${NC}             Run all tests
  ${BLUE}test-watch${NC}       Run tests in watch mode
  ${BLUE}doctor${NC}           Check dependencies and system health
  ${BLUE}db migrate${NC}       Apply database migrations
  ${BLUE}db seed${NC}          Seed database with sample data
  ${BLUE}db reset${NC}         Reset database (drop and recreate)
  ${BLUE}db status${NC}        Show database migration status
  ${BLUE}clean${NC}            Remove build artifacts and caches
  ${BLUE}logs${NC}             Show Docker logs (optional: service name)
  ${BLUE}ps${NC}               Show running containers
  ${BLUE}help${NC}             Show this help message

${GREEN}Examples:${NC}
  ./run.sh dev                    # Start development environment
  ./run.sh test                   # Run tests
  ./run.sh db migrate             # Apply migrations
  ./run.sh logs backend           # Show backend logs
  ./run.sh doctor                 # Check system health

${GREEN}Development Workflow:${NC}
  1. ./run.sh doctor              # Check prerequisites
  2. ./run.sh dev                 # Start services
  3. ./run.sh logs                # Monitor logs
  4. ./run.sh stop                # Stop when done

EOF
}

# Doctor command - check dependencies
cmd_doctor() {
    print_header "System Health Check"

    local all_good=true

    # Check .NET SDK
    if command_exists dotnet; then
        local dotnet_version=$(dotnet --version)
        print_success ".NET SDK: $dotnet_version"
        if [[ ! "$dotnet_version" =~ ^9\. ]]; then
            print_warning ".NET 9 recommended (found: $dotnet_version)"
        fi
    else
        print_error ".NET SDK not found"
        all_good=false
    fi

    # Check Docker
    if command_exists docker; then
        local docker_version=$(docker --version | grep -oP '\d+\.\d+\.\d+' | head -1)
        print_success "Docker: $docker_version"

        if docker ps >/dev/null 2>&1; then
            print_success "Docker daemon is running"
        else
            print_error "Docker daemon is not running"
            all_good=false
        fi
    else
        print_error "Docker not found"
        all_good=false
    fi

    # Check Docker Compose
    if command_exists docker && docker compose version >/dev/null 2>&1; then
        local compose_version=$(docker compose version --short)
        print_success "Docker Compose: $compose_version"
    else
        print_error "Docker Compose not found"
        all_good=false
    fi

    # Check Node.js
    if command_exists node; then
        local node_version=$(node --version)
        print_success "Node.js: $node_version"
    else
        print_warning "Node.js not found (required for frontend development)"
    fi

    # Check npm
    if command_exists npm; then
        local npm_version=$(npm --version)
        print_success "npm: $npm_version"
    else
        print_warning "npm not found (required for frontend development)"
    fi

    # Check Git
    if command_exists git; then
        local git_version=$(git --version | grep -oP '\d+\.\d+\.\d+')
        print_success "Git: $git_version"
    else
        print_warning "Git not found"
    fi

    echo ""

    # Check project files
    print_info "Checking project structure..."

    if [[ -f "ResearchAgentNetwork.sln" ]]; then
        print_success "Solution file found"
    else
        print_error "ResearchAgentNetwork.sln not found"
        all_good=false
    fi

    if [[ -f "docker-compose.yml" ]]; then
        print_success "docker-compose.yml found"
    else
        print_error "docker-compose.yml not found"
        all_good=false
    fi

    if [[ -f "docker-compose.dev.yml" ]]; then
        print_success "docker-compose.dev.yml found"
    else
        print_warning "docker-compose.dev.yml not found (optional for dev mode)"
    fi

    echo ""

    if $all_good; then
        print_success "All critical dependencies satisfied!"
        print_info "You're ready to start: ./run.sh dev"
    else
        print_error "Some dependencies are missing. Please install them first."
        return 1
    fi
}

# Dev command - start development environment
cmd_dev() {
    print_header "Starting Development Environment"

    print_info "Starting services with hot reload enabled..."
    docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
}

# Prod command - start production environment
cmd_prod() {
    print_header "Starting Production Environment"

    print_info "Starting optimized production services..."
    docker compose up --build -d

    echo ""
    print_success "Services started!"
    print_info "Frontend: http://localhost:5173"
    print_info "Backend:  http://localhost:5000"
    print_info "Ollama:   http://localhost:11434"
    print_info "Qdrant:   http://localhost:6333/dashboard"
    echo ""
    print_info "View logs: ./run.sh logs"
    print_info "Stop:      ./run.sh stop"
}

# Stop command
cmd_stop() {
    print_header "Stopping Services"
    docker compose stop
    print_success "All services stopped"
}

# Down command
cmd_down() {
    print_header "Removing Containers"
    print_warning "This will remove containers and networks (volumes preserved)"
    docker compose down
    print_success "Cleanup complete"
}

# Build command
cmd_build() {
    print_header "Building .NET Solution"
    dotnet build
    print_success "Build complete"
}

# Test command
cmd_test() {
    print_header "Running Tests"
    dotnet test --verbosity normal
}

# Test watch command
cmd_test_watch() {
    print_header "Running Tests (Watch Mode)"
    print_info "Press Ctrl+C to stop..."
    dotnet watch test --project ResearchAgentNetwork.Tests
}

# Database migrate command
cmd_db_migrate() {
    print_header "Applying Database Migrations"

    if docker compose ps backend | grep -q "Up"; then
        print_info "Running migrations in container..."
        docker compose exec backend dotnet ef database update --project /app/ResearchAgentNetwork.Persistence --startup-project /app/ResearchAgentNetwork.Web
    else
        print_info "Running migrations locally..."
        dotnet ef database update --project ResearchAgentNetwork.Persistence --startup-project ResearchAgentNetwork.Web
    fi

    print_success "Migrations applied"
}

# Database seed command
cmd_db_seed() {
    print_header "Seeding Database"
    print_warning "Database seeding not yet implemented"
    print_info "You can submit tasks via API: POST http://localhost:5000/api/tasks"
}

# Database reset command
cmd_db_reset() {
    print_header "Resetting Database"
    print_warning "This will DELETE all data!"
    read -p "Are you sure? (yes/no): " -r

    if [[ "$REPLY" == "yes" ]]; then
        print_info "Removing database files..."
        rm -f ran.db ran.db-shm ran.db-wal

        print_info "Applying migrations..."
        cmd_db_migrate

        print_success "Database reset complete"
    else
        print_info "Reset cancelled"
    fi
}

# Database status command
cmd_db_status() {
    print_header "Database Migration Status"

    if docker compose ps backend | grep -q "Up"; then
        docker compose exec backend dotnet ef migrations list --project /app/ResearchAgentNetwork.Persistence --startup-project /app/ResearchAgentNetwork.Web
    else
        dotnet ef migrations list --project ResearchAgentNetwork.Persistence --startup-project ResearchAgentNetwork.Web
    fi
}

# Clean command
cmd_clean() {
    print_header "Cleaning Build Artifacts"

    print_info "Removing bin/ and obj/ directories..."
    find . -type d -name "bin" -o -name "obj" | xargs rm -rf

    print_info "Removing node_modules/ directories..."
    find . -type d -name "node_modules" | xargs rm -rf

    print_info "Removing build caches..."
    rm -rf ui/app/.svelte-kit
    rm -rf ui-react-router/dist

    print_success "Cleanup complete"
}

# Logs command
cmd_logs() {
    local service=$1

    if [[ -n "$service" ]]; then
        print_header "Logs: $service"
        docker compose logs -f "$service"
    else
        print_header "Logs: All Services"
        docker compose logs -f
    fi
}

# PS command
cmd_ps() {
    print_header "Running Containers"
    docker compose ps
}

# Main command dispatcher
main() {
    if [[ $# -eq 0 ]]; then
        show_usage
        exit 0
    fi

    local command=$1
    shift

    case "$command" in
        dev)
            cmd_dev "$@"
            ;;
        prod)
            cmd_prod "$@"
            ;;
        stop)
            cmd_stop "$@"
            ;;
        down)
            cmd_down "$@"
            ;;
        build)
            cmd_build "$@"
            ;;
        test)
            cmd_test "$@"
            ;;
        test-watch)
            cmd_test_watch "$@"
            ;;
        doctor)
            cmd_doctor "$@"
            ;;
        db)
            local db_command=$1
            shift
            case "$db_command" in
                migrate)
                    cmd_db_migrate "$@"
                    ;;
                seed)
                    cmd_db_seed "$@"
                    ;;
                reset)
                    cmd_db_reset "$@"
                    ;;
                status)
                    cmd_db_status "$@"
                    ;;
                *)
                    print_error "Unknown db command: $db_command"
                    echo ""
                    echo "Available db commands: migrate, seed, reset, status"
                    exit 1
                    ;;
            esac
            ;;
        clean)
            cmd_clean "$@"
            ;;
        logs)
            cmd_logs "$@"
            ;;
        ps)
            cmd_ps "$@"
            ;;
        help|--help|-h)
            show_usage
            ;;
        *)
            print_error "Unknown command: $command"
            echo ""
            show_usage
            exit 1
            ;;
    esac
}

# Run main function
main "$@"

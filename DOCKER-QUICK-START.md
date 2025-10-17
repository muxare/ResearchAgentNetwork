# Docker Quick Start Guide

## TL;DR - Get Running in 5 Minutes

```bash
# Clone and navigate to project
cd C:\source\repos\Lab\SematicKernel\ResearchAgentNetwork

# Start everything
docker compose up

# Wait for services to initialize (~2-3 minutes first time)
# Access frontend: http://localhost:5173
# Access backend API: http://localhost:5000
```

---

## What Gets Started?

When you run `docker compose up`, you get:

1. **Backend API** (.NET 9) - `http://localhost:5000`
2. **Frontend UI** (SvelteKit) - `http://localhost:5173`
3. **Ollama** (LLM provider) - `http://localhost:11434`
4. **Qdrant** (Vector DB) - `http://localhost:6333` (HTTP), `6334` (gRPC)
5. **Ollama Init** - Automatically downloads `llama3.1:latest` and `nomic-embed-text`

All services are networked together and health-checked automatically!

---

## Prerequisites

- **Docker Desktop** for Windows (with WSL 2 backend recommended)
  - Download: https://www.docker.com/products/docker-desktop/
- **8GB+ RAM** (16GB recommended for comfortable development)
- **20GB+ free disk space** (for Docker images and Ollama models)

### Optional but Recommended:
- **NVIDIA GPU** + nvidia-docker for GPU acceleration (see GPU setup below)

---

## Quick Commands

### Using the Unified CLI (Recommended)

We provide cross-platform CLI scripts ([run.sh](run.sh) / [run.ps1](run.ps1)) that simplify common operations:

```bash
# Windows
.\run.ps1 doctor      # Check system health
.\run.ps1 dev         # Start development environment
.\run.ps1 prod        # Start production environment
.\run.ps1 test        # Run tests
.\run.ps1 logs        # View logs
.\run.ps1 stop        # Stop services
.\run.ps1 help        # Show all commands

# Linux/Mac
./run.sh doctor
./run.sh dev
./run.sh prod
# ... etc
```

For full command reference, run `.\run.ps1 help` or `./run.sh help`.

### Using Docker Compose Directly

```bash
# Production mode (optimized builds)
docker compose up

# Development mode (with hot reload)
docker compose -f docker-compose.yml -f docker-compose.dev.yml up

# Detached mode (run in background)
docker compose up -d

# Rebuild and start (after code changes to Dockerfile)
docker compose up --build
```

### Stop Services

```bash
# Stop containers (keeps data)
docker compose down

# Stop and remove volumes (DELETES ALL DATA)
docker compose down -v
```

### View Logs

```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f backend
docker compose logs -f ollama
docker compose logs -f frontend

# Last 100 lines
docker compose logs --tail=100 backend
```

### Check Status

```bash
# List running containers
docker compose ps

# Check service health
docker compose ps --format "table {{.Service}}\t{{.Status}}\t{{.Ports}}"
```

### Rebuild After Changes

```bash
# Rebuild backend only
docker compose build backend

# Rebuild frontend only
docker compose build frontend

# Rebuild everything
docker compose build
```

---

## First-Time Setup

### 1. Start Services

```bash
docker compose up
```

### 2. Wait for Initialization

**First startup takes 2-5 minutes** depending on your internet speed:

1. **Pulling Docker images** (~2 min)
   - .NET SDK (large!)
   - Node.js
   - Ollama
   - Qdrant

2. **Building backend** (~1 min)
   - Compiling .NET solution

3. **Downloading LLM models** (~1-2 min)
   - llama3.1:latest (~4.7GB)
   - nomic-embed-text (~274MB)

You'll see progress in the logs:
```
ollama-init  | Pulling llama3.1:latest model...
ollama-init  | pulling manifest
ollama-init  | pulling 8eeb52dfb3bb... 100%
ollama-init  | Model pull complete!
```

### 3. Verify Services Are Healthy

```bash
# Check all services are up
docker compose ps

# Expected output:
# NAME                                  STATUS
# researchagentnetwork-backend-1        Up (healthy)
# researchagentnetwork-ollama-1         Up (healthy)
# researchagentnetwork-qdrant-1         Up (healthy)
# researchagentnetwork-frontend-1       Up
# researchagentnetwork-ollama-init-1    Exited (0)  # This is expected!
```

### 4. Access the Application

- **Frontend:** http://localhost:5173
- **Backend API:** http://localhost:5000
- **Backend Health Check:** http://localhost:5000/health
- **Ollama API:** http://localhost:11434/api/tags
- **Qdrant Dashboard:** http://localhost:6333/dashboard

---

## Development Workflow

### Hot Reload (Development Mode)

Start with development overrides for automatic code reloading:

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up
```

**What you get:**
- **Backend hot reload:** Edit C# files → auto-recompile
- **Frontend hot reload:** Edit Svelte files → instant browser update
- **Verbose logging:** See all LLM prompts and responses

### Making Code Changes

#### Backend Changes (.NET)

1. Edit code in `ResearchAgentNetwork.Core/`, `ResearchAgentNetwork.Web/`, etc.
2. In development mode, `dotnet watch` will **auto-detect and rebuild**
3. Refresh browser to see changes

#### Frontend Changes (Svelte)

1. Edit files in `ui/app/src/`
2. Vite dev server will **auto-reload browser instantly**
3. No manual refresh needed!

#### Configuration Changes

1. Edit `appsettings.json` or environment variables in `docker-compose.yml`
2. Restart services: `docker compose restart backend`

### Adding NuGet or npm Packages

**Backend (.NET):**
```bash
# Stop containers
docker compose down

# Add package (outside container)
cd ResearchAgentNetwork.Web
dotnet add package PackageName

# Rebuild
docker compose build backend
docker compose up
```

**Frontend (Node):**
```bash
# Add package (outside container or exec into container)
cd ui/app
npm install package-name

# Restart frontend
docker compose restart frontend
```

---

## Troubleshooting

### Issue: Backend fails to start with "Ollama not reachable"

**Symptoms:**
```
backend  | ⚠️ Ollama not reachable at: ollama:11434
```

**Fix:**
1. Check Ollama is healthy: `docker compose ps ollama`
2. If not healthy, check logs: `docker compose logs ollama`
3. Restart Ollama: `docker compose restart ollama`
4. Wait for health check to pass (~30 seconds)

### Issue: "Port already in use" error

**Symptoms:**
```
Error response from daemon: Ports are not available: exposing port TCP 0.0.0.0:5000
```

**Fix:**
1. Check what's using the port:
   ```bash
   netstat -ano | findstr :5000
   ```
2. Kill the process or change port in `docker-compose.yml`:
   ```yaml
   backend:
     ports:
       - "5001:8080"  # Use 5001 instead
   ```

### Issue: Ollama models not downloading

**Symptoms:**
```
ollama-init  | Error: connection refused
```

**Fix:**
1. Ensure Ollama service is healthy first
2. Manually pull models:
   ```bash
   docker compose exec ollama ollama pull llama3.1:latest
   docker compose exec ollama ollama pull nomic-embed-text
   ```

### Issue: Frontend can't connect to backend

**Symptoms:**
Frontend shows connection errors, CORS errors

**Fix:**
1. Verify backend is running: `curl http://localhost:5000/health`
2. Check `VITE_API_URL` environment variable in `docker-compose.yml`
3. Rebuild frontend: `docker compose build frontend && docker compose up frontend`

### Issue: "Permission denied" errors on Linux/Mac

**Fix:**
```bash
# Give Docker permissions
sudo chmod -R 777 backend-data/
```

### Issue: Out of disk space

**Symptoms:**
```
ERROR: no space left on device
```

**Fix:**
```bash
# Remove unused Docker data
docker system prune -a --volumes

# WARNING: This deletes ALL unused Docker images and volumes!
# Your Ollama models will need to be re-downloaded
```

### Issue: Backend crashes with database errors

**Fix:**
```bash
# Delete SQLite database and restart
docker compose down
docker volume rm researchagentnetwork_backend-data
docker compose up
```

---

## GPU Acceleration (NVIDIA)

If you have an NVIDIA GPU, you can enable GPU acceleration for Ollama:

### Prerequisites

1. **Install NVIDIA drivers**
2. **Install nvidia-docker:**
   ```bash
   # Follow instructions at: https://github.com/NVIDIA/nvidia-docker
   ```

### Enable GPU in Docker Compose

Edit `docker-compose.yml` and **uncomment** these lines:

```yaml
ollama:
  deploy:
    resources:
      reservations:
        devices:
          - driver: nvidia
            count: 1
            capabilities: [gpu]
```

Restart services:
```bash
docker compose down
docker compose up
```

Verify GPU is being used:
```bash
docker compose exec ollama nvidia-smi
```

---

## Advanced Usage

### Using PostgreSQL Instead of SQLite

Uncomment the PostgreSQL service in `docker-compose.dev.yml`:

```yaml
postgres:
  image: postgres:15-alpine
  # ... (already configured, just uncomment)
```

Update backend environment:
```yaml
backend:
  environment:
    - Database__Provider=SqlServer
    - Database__ConnectionString=Host=postgres;Database=ran_dev;Username=ranuser;Password=ranpass_dev
```

### Scaling Services

```bash
# Run 3 backend instances (requires load balancer)
docker compose up --scale backend=3
```

### Accessing Containers

```bash
# Open shell in backend container
docker compose exec backend bash

# Open shell in frontend container
docker compose exec frontend sh

# Open shell in Ollama container
docker compose exec ollama bash
```

### Viewing Resource Usage

```bash
# Real-time stats
docker stats

# Specific service
docker stats researchagentnetwork-backend-1
```

---

## Data Persistence

Docker volumes store data that persists across container restarts:

- **ollama-data:** Stores downloaded LLM models (~5GB)
- **qdrant-data:** Stores vector database indexes
- **backend-data:** Stores SQLite database and files

### Backup Data

```bash
# Backup Ollama models
docker compose exec ollama tar czf /tmp/ollama-backup.tar.gz /root/.ollama
docker compose cp ollama:/tmp/ollama-backup.tar.gz ./ollama-backup.tar.gz

# Backup Qdrant data
docker compose exec qdrant tar czf /tmp/qdrant-backup.tar.gz /qdrant/storage
docker compose cp qdrant:/tmp/qdrant-backup.tar.gz ./qdrant-backup.tar.gz
```

### Restore Data

```bash
# Restore Ollama models
docker compose cp ./ollama-backup.tar.gz ollama:/tmp/
docker compose exec ollama tar xzf /tmp/ollama-backup.tar.gz -C /
```

---

## Environment Variables Reference

Key environment variables you can override in `docker-compose.yml`:

### Backend

```yaml
# AI Provider
- AI_PROVIDER=Ollama|OpenAI|AzureOpenAI

# Ollama Config
- Ollama__Endpoint=http://ollama:11434
- Ollama__ModelId=llama3.1:latest
- Ollama__EmbeddingModelId=nomic-embed-text

# Vector Database
- VectorDb__Provider=Qdrant|None
- VectorDb__Endpoint=qdrant:6334
- VectorDb__CollectionPrefix=ran

# Research Agent
- ResearchAgent__MaxConcurrency=5
- ResearchAgent__MaxDecompositionDepth=2
- ResearchAgent__LogPrompts=true
- ResearchAgent__EnableWebSearch=false

# Database
- Database__Provider=Sqlite|SqlServer
```

### Frontend

```yaml
- VITE_API_URL=http://localhost:5000
- VITE_LOG_LEVEL=debug|info|warn|error
```

---

## Performance Tips

1. **Allocate more memory to Docker Desktop:**
   - Settings → Resources → Memory → 8GB+

2. **Use WSL 2 backend on Windows:**
   - Significantly faster than Hyper-V

3. **Enable BuildKit:**
   ```bash
   $env:DOCKER_BUILDKIT=1
   docker compose build
   ```

4. **Pre-pull images:**
   ```bash
   docker compose pull
   ```

5. **Use development mode only when actively coding:**
   - Production mode is faster and uses less resources

---

## Clean Up

### Remove Everything

```bash
# Stop containers
docker compose down

# Remove all project containers, networks, volumes
docker compose down -v --remove-orphans

# Remove all images
docker rmi $(docker images 'researchagentnetwork*' -q)
```

### Start Fresh

```bash
# Complete reset
docker compose down -v
docker system prune -a
docker compose up --build
```

**⚠️ WARNING:** This deletes ALL data including Ollama models (5GB+ download)!

---

## Next Steps

Once Docker is running:

1. **Submit a test task:** http://localhost:5173
2. **Check backend logs:** `docker compose logs -f backend`
3. **Monitor Ollama:** `docker compose logs -f ollama`
4. **Explore Qdrant:** http://localhost:6333/dashboard

For more advanced configuration, see the main `README.md`.

---

## Getting Help

**Check service logs:**
```bash
docker compose logs [service-name]
```

**Check service health:**
```bash
docker compose ps
```

**Verify Docker setup:**
```bash
docker version
docker compose version
```

**Test individual services:**
```bash
curl http://localhost:5000/health
curl http://localhost:11434/api/tags
curl http://localhost:6333/health
```

Still stuck? Open an issue on GitHub with:
- Output of `docker compose logs`
- Output of `docker compose ps`
- Your OS and Docker version

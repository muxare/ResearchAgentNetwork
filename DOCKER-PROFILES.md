# Docker Compose Profiles Guide

This project uses **Docker Compose Profiles** to run different backend implementations with the same frontend UI.

## Quick Start

### Start .NET Backend (Semantic Kernel)
```powershell
.\run.ps1 dotnet
```
- Starts .NET backend on port 5000
- Frontend connects to .NET backend automatically
- Access frontend at: http://localhost:5173

### Start Python Backend (LangGraph)
```powershell
.\run.ps1 python
```
- Starts Python backend on port 8090
- Frontend connects to Python backend automatically
- Access frontend at: http://localhost:5173

### Start BOTH Backends (Comparison Mode)
```powershell
.\run.ps1 both
```
- Starts .NET backend on port 5000
- Starts Python backend on port 8090
- Frontend defaults to .NET backend
- Access frontend at: http://localhost:5173
- Test both APIs side-by-side for comparison

## Profile Architecture

```
┌─────────────────────────────────────────────────────────┐
│                  Shared Infrastructure                  │
│  (Always starts, no profile needed)                    │
│  - Ollama (LLM)                                        │
│  - Qdrant (Vector DB)                                  │
│  - ollama-init (Model downloader)                      │
└─────────────────────────────────────────────────────────┘
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
        ▼                  ▼                  ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│Profile:dotnet│  │Profile:python│  │Profile: both │
├──────────────┤  ├──────────────┤  ├──────────────┤
│ backend      │  │python-backend│  │ backend      │
│frontend-dotnet│ │frontend-python│ │python-backend│
│              │  │              │  │frontend-both │
└──────────────┘  └──────────────┘  └──────────────┘
```

## Service Mapping

| Profile | Backend Service | Frontend Service | Backend URL | Frontend URL |
|---------|----------------|------------------|-------------|--------------|
| `dotnet` | `backend` | `frontend-dotnet` | http://backend:8080 | http://localhost:5173 |
| `python` | `python-backend` | `frontend-python` | http://python-backend:8090 | http://localhost:5173 |
| `both` | `backend` + `python-backend` | `frontend-both` | Both available | http://localhost:5173 |

## Advanced Usage

### Using Docker Compose Directly

```powershell
# Start .NET stack
docker compose --profile dotnet up

# Start Python stack
docker compose --profile python up

# Start both (comparison mode)
docker compose --profile both up

# Start only shared services (no backend/frontend)
docker compose up
```

### View Running Services

```powershell
.\run.ps1 ps
# or
docker compose ps
```

### View Logs

```powershell
# .NET backend logs
.\run.ps1 logs backend

# Python backend logs
.\run.ps1 logs python-backend

# Frontend logs
.\run.ps1 logs frontend-dotnet
# or
.\run.ps1 logs frontend-python
# or
.\run.ps1 logs frontend-both

# All services
.\run.ps1 logs
```

### Stop Services

```powershell
.\run.ps1 stop    # Stop all containers
.\run.ps1 down    # Stop and remove containers
```

## Why Profiles?

### Before Profiles (Manual Switching)
```powershell
# Had to manually set environment variable
$env:BACKEND_URL="http://python-backend:8090"
docker compose up
```

### After Profiles (Automatic)
```powershell
# Frontend automatically connects to right backend
.\run.ps1 python
```

### Benefits
1. ✅ **No manual configuration** - Frontend automatically connects to correct backend
2. ✅ **Single command** - `.\run.ps1 dotnet` or `.\run.ps1 python`
3. ✅ **Run both simultaneously** - Compare implementations side-by-side
4. ✅ **Clear intent** - Command name indicates which backend runs
5. ✅ **Resource efficient** - Only starts services you need

## Use Cases

### Development on .NET Backend
```powershell
.\run.ps1 dotnet
# Edit .NET code, test with frontend
```

### Development on Python Backend
```powershell
.\run.ps1 python
# Edit Python code, test with frontend
```

### A/B Testing / Comparison
```powershell
.\run.ps1 both
# Submit same task to both backends:
curl -X POST http://localhost:5000/api/tasks -d '{"query":"test"}'  # .NET
curl -X POST http://localhost:8090/api/tasks -d '{"query":"test"}'  # Python
# Compare results
```

### CI/CD Testing
```yaml
# Test .NET stack
- run: docker compose --profile dotnet up -d
- run: npm run test:e2e -- --api-url=http://localhost:5000

# Test Python stack
- run: docker compose --profile python up -d
- run: npm run test:e2e -- --api-url=http://localhost:8090
```

## Troubleshooting

### Frontend not connecting to backend?
```powershell
# Check which services are running
.\run.ps1 ps

# Check frontend environment variable
docker compose exec frontend-dotnet env | grep VITE_API_URL
# Should show: VITE_API_URL=http://backend:8080

docker compose exec frontend-python env | grep VITE_API_URL
# Should show: VITE_API_URL=http://python-backend:8090
```

### Backend not starting?
```powershell
# Check logs
.\run.ps1 logs backend        # .NET
.\run.ps1 logs python-backend # Python

# Verify health
docker compose ps
# All services should show "(healthy)"
```

### Want to rebuild from scratch?
```powershell
.\run.ps1 down
docker compose --profile dotnet up --build
# or
docker compose --profile python up --build
```

## Configuration

The profile system is configured in [docker-compose.yml](docker-compose.yml):

```yaml
services:
  backend:
    profiles: ["dotnet", "both"]  # Starts with dotnet or both profile
    # ...

  python-backend:
    profiles: ["python", "both"]  # Starts with python or both profile
    # ...

  frontend-dotnet:
    profiles: ["dotnet"]           # Only starts with dotnet profile
    environment:
      - VITE_API_URL=http://backend:8080
    # ...

  frontend-python:
    profiles: ["python"]           # Only starts with python profile
    environment:
      - VITE_API_URL=http://python-backend:8090
    # ...

  frontend-both:
    profiles: ["both"]             # Only starts with both profile
    environment:
      - VITE_API_URL=${BACKEND_URL:-http://backend:8080}
    # ...
```

## See Also

- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [Docker Compose Profiles](https://docs.docker.com/compose/profiles/)
- [DOCKER-QUICK-START.md](DOCKER-QUICK-START.md) - General Docker setup guide
- [SHORT-PLAN.md](docs/SHORT-PLAN.md) - Phase 2: Python/LangGraph implementation plan

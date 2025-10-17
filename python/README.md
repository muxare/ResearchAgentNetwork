# Research Agent Network - Python/LangGraph Implementation

This is a parallel Python implementation of the Research Agent Network using **LangGraph** for graph-based orchestration. It coexists with the .NET Semantic Kernel implementation and shares the same database.

## 🎯 LangGraph-First Philosophy

This implementation focuses exclusively on **LangGraph primitives** to maximize learning of graph-based orchestration. We intentionally minimize LangChain usage to only essential components.

### ✅ ALLOWED Dependencies

**LangGraph Core (Primary Focus):**
- `langgraph` - Graph orchestration, state management, checkpoints, streaming
- `langchain-core` - **ONLY** for: `BaseMessage`, `ChatPromptTemplate`, `RunnableConfig`
- `langchain-ollama` - **ONLY** for: `ChatOllama` LLM wrapper

**Direct API Clients (No LangChain Wrappers):**
- `httpx` - For Tavily web search (direct API calls)
- `qdrant-client` - For vector store (direct API calls)
- `sqlalchemy` - For database ORM
- `pydantic` - For data validation
- `fastapi` / `uvicorn` - For web API

### ❌ AVOID These LangChain Components

**NEVER use these packages/modules:**
- ❌ `langchain` - Main package (too broad, not graph-focused)
- ❌ `langchain.agents` - Agent framework (LangGraph replaces this)
- ❌ `langchain.chains` - Chain abstractions (LangGraph replaces this)
- ❌ `langchain.tools` - Tool wrappers (use direct API calls)
- ❌ `langchain.memory` - Memory classes (use LangGraph state + checkpoints)

## Quick Start

### Using Docker Compose (Recommended)

```bash
# From repository root
docker-compose up python-backend
```

### Local Development

```bash
# Navigate to python directory
cd python

# Install dependencies
pip install -r requirements.txt

# Copy environment file
cp .env.example .env

# Run the API server
uvicorn ran_py.api.app:app --reload --port 8090
```

## Project Structure

```
python/
├── ran_py/                    # Main package
│   ├── models/                # Pydantic schemas (match .NET)
│   ├── agents/                # Agent business logic
│   ├── graphs/                # LangGraph definitions
│   ├── db/                    # SQLAlchemy (shared DB with .NET)
│   ├── llm/                   # LLM provider wrappers
│   ├── tools/                 # Minimal tool functions
│   └── api/                   # FastAPI application
└── tests/                     # Unit + integration tests
```

## Running Tests

```bash
# Run all tests
pytest

# Run only unit tests
pytest -m unit

# Run with coverage report
pytest --cov=ran_py --cov-report=html
```

## API Endpoints (Phase 2 Priority)

- `POST /api/tasks` - Submit new research task
- `GET /api/tasks/{id}` - Get task status
- `GET /api/tasks/{id}/events` - Get task timeline
- `GET /api/events` - SSE stream of real-time updates

## Shared Database

Python and .NET share the same SQLite database (`data/ran.db`):
- SQLAlchemy models use **PascalCase** column names (match EF Core)
- Both systems read/write `Tasks`, `TaskEvents`, `TaskReports` tables
- Repository pattern mirrors .NET `ITaskRepository` interface

## Development Guidelines

1. **State Over Memory:** Use LangGraph `State` classes with typed fields
2. **Graphs Over Chains:** Use LangGraph `StateGraph`, not LangChain chains
3. **Nodes Over Tools:** Write simple async functions as graph nodes
4. **Checkpoints Over History:** Use LangGraph checkpointers (SQLite/Postgres)
5. **Direct APIs Over Wrappers:** Call external APIs directly (Tavily, Qdrant)

## Learn More

- [LangGraph Documentation](https://langchain-ai.github.io/langgraph/)
- [Main Project Documentation](../docs/)
- [Phase 2 Implementation Plan](../docs/SHORT-PLAN.md)

## Python + LangGraph Implementation Plan for ResearchAgentNetwork (v1 - SUPERSEDED)

> **⚠️ NOTE:** This document has been superseded by **LangGraph-Implementation-Plan-v2.md**, which includes:
> - Full feature parity mapping to .NET system
> - Modern LangGraph features (streaming, checkpointing, LangSmith)
> - Production deployment and scaling strategies
> - Concrete code examples for all components
> - Comprehensive testing and migration strategies
>
> **Please refer to v2 for the current implementation plan.**

---

This document outlines how to implement the ResearchAgentNetwork using Python with LangGraph while coexisting with the current .NET/Semantic Kernel system. It covers the architecture, requirements, data and execution flow, testing, repo layout, rollout phases, and operational concerns. The plan adheres to the user's implementation guidelines (small PRs, clear phases, independent steps, and reviewability).

### Goals and Scope
- **Goal**: Stand up a Python service that implements the core agentic orchestration with LangGraph while keeping the existing UI(s) and data stores compatible.
- **Scope (Phase 1)**: Minimal end-to-end agent flow (Task Analyzer → Planner → WebSearch → Synthesizer → Finalizer) with persistent memory and vector search. Provide a small FastAPI wrapper for API parity where practical.
- **Non-Goals (Phase 1)**: Full parity with all C# features; deep Microsoft-specific integrations; heavy UI rewrites.

### Guiding Principles
- **Local-first LLM via Ollama** for development and default runtime [[memory:5990558]].
- **Incremental adoption**: Run Python service alongside the .NET services; swap or proxy endpoints gradually.
- **Composable nodes**: Small, testable LangGraph nodes; shared data contracts with Pydantic models.
- **Deterministic edges**: Clear graph control flow; error edges and retries explicitly modeled.
- **Observability**: Structured logging, tracing, and metrics from day one.

---

## High-Level Architecture

### Components
- **LangGraph Orchestrator Service (Python)**
  - Hosts graphs for: Task Analysis, Query Planning, Web Search, Deduplication, Synthesis, Fact-Check, and Finalization.
  - Exposes HTTP endpoints via FastAPI to trigger runs, inspect graph state, and retrieve artifacts.
- **Tools**
  - Web search tool(s) with allowlist/rate-limit controls.
  - Vector store tool for semantic memory (Qdrant recommended per existing project docs).
  - File/Blob storage for artifacts (local filesystem in Phase 1; later S3/Azure).
- **LLM Providers**
  - Ollama running locally (e.g., `llama3`, `llama3.1`, `mistral`) with a thin adapter layer.
- **Persistence**
  - Vector DB: Qdrant (local docker) for embeddings and retrieval.
  - Relational: Optional—start without, or reuse existing SQLite/db if needed for audit logs.
- **UI**
  - Keep SvelteKit/React Router UI unchanged initially. Interact with Python API endpoints for graph runs, progress, and results.

### Data Contracts
Use Pydantic models for requests/responses and graph state. Map to existing domain concepts where possible (e.g., `Task`, `TaskPlan`, `SearchResult`, `SectionDraft`, `FinalReport`).

---

## Execution Flow (Phase 1 Minimal E2E)
1. Client posts a `Task` to Python API `/api/ran/graph/run` with objective, constraints, and optional context.
2. Graph initializes state: task, memory context refs, telemetry IDs.
3. Nodes execute in sequence (with guards):
   - TaskAnalyzer → outputs clarified objective and key intents.
   - Planner → generates query plan and section outline.
   - WebSearch → executes queries; retrieves, filters, and ranks results.
   - Deduplicator → clusters/merges similar content.
   - Synthesizer → drafts sections from curated sources.
   - Finalizer → stitches sections; returns final report + citations.
4. Artifacts and intermediate states are persisted to vector store and optional disk.
5. API returns the final artifact ID and a summary; client can fetch details or stream updates.

Error handling: Each node has an error edge with retry/backoff and minimal breadcrumbs for debugging. Non-recoverable errors terminate with a structured failure payload.

---

## LangGraph Design

### Graph Structure
- **State**: `GraphState` Pydantic model with fields like `objective`, `plan`, `queries`, `results`, `sections`, `citations`, `final_report`, `errors`, `telemetry`.
- **Nodes** (pure functions or classes):
  - `task_analyzer_node`
  - `planner_node`
  - `web_search_node`
  - `deduplicate_node`
  - `synthesize_node`
  - `finalize_node`
  - `checkpoint_node` (optional; persists state safely)
- **Edges**: Linear for Phase 1; add conditional edges later (e.g., skip search if existing memory is rich, loop planner if gaps found).

### Memory and Retrieval
- **Embeddings**: Use an Ollama-compatible embedding model or `nomic-embed-text` via `sentence-transformers` as fallback.
- **Vector Store**: Qdrant (Docker) with collections for: `tasks`, `sources`, `chunks`, `sections`, `reports`.
- **RAG**: Planner produces search queries; retrieved docs chunked and embedded; synthesizer conditions on top-k chunks with citations.

### LLM Abstraction
- Thin provider wrapper targeting **Ollama** by default [[memory:5990558]]; configurable via env var. Provide adapters for chat/completions and tool-calling where needed.

---

## Requirements

### Runtime
- Python 3.11+
- Windows and Linux supported (dev on Windows PowerShell)

### Core Dependencies (pinned examples)
```
langgraph==0.2.22
langchain==0.2.16
langchain-community==0.2.16
fastapi==0.114.0
uvicorn[standard]==0.30.6
pydantic==2.8.2
httpx==0.27.0
tenacity==8.5.0
qdrant-client==1.10.1
sentence-transformers==3.0.1
python-dotenv==1.0.1
structlog==24.1.0
opentelemetry-sdk==1.26.0
opentelemetry-exporter-otlp==1.26.0
```

### System Services
- Ollama running locally with at least one chat model downloaded (e.g., `llama3:latest`).
- Qdrant via Docker (default `localhost:6333`).

### Environment Variables
- `OLLAMA_BASE_URL` (default `http://localhost:11434`)
- `RAN_PY_LOG_LEVEL` (e.g., `INFO`)
- `QDRANT_URL`, `QDRANT_API_KEY` (if secured)
- `RAN_PY_MODEL` (default `llama3`)

### Dev Setup (Windows PowerShell)
```
cd C:\source\repos\Lab\SematicKernel\ResearchAgentNetwork
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -U pip
pip install -r python\requirements.txt

# Start services (in separate terminals)
ollama serve
docker run -p 6333:6333 qdrant/qdrant:latest

# Run API
uvicorn ran_py.api.app:app --reload --port 8090
```

---

## Proposed Repo Layout (Python Side)
```
python/
  requirements.txt
  ran_py/
    __init__.py
    config.py
    logging.py
    models/              # pydantic schemas for state and IO
      __init__.py
      task.py
      plan.py
      search.py
      report.py
    llm/
      __init__.py
      ollama_provider.py
    tools/
      __init__.py
      web_search.py
      vectors.py
    graph/
      __init__.py
      state.py
      nodes/
        __init__.py
        task_analyzer.py
        planner.py
        web_search.py
        deduplicate.py
        synthesize.py
        finalize.py
      build.py           # constructs LangGraph
    api/
      __init__.py
      app.py             # FastAPI app
    tests/
      test_nodes_*.py
      test_graph_e2e.py
```

This layout keeps nodes small and testable, isolates adapters, and provides a single FastAPI entrypoint.

---

## API Design (Phase 1)
- `POST /api/ran/graph/run`: Start a run with a `Task` payload → returns `run_id` and initial state.
- `GET /api/ran/graph/run/{run_id}`: Fetch current state and final artifact if completed.
- `GET /api/ran/graph/run/{run_id}/events`: Optional server-sent events for live updates.

These endpoints are additive; existing .NET endpoints remain unchanged. The UI can be pointed to these endpoints for Python-powered runs without disrupting legacy flows.

---

## Testing Strategy
- **Unit tests** for each node (pure transform focus). Use `pytest` and stub LLM/tool IO.
- **Integration tests** with a lightweight test graph and a Fake/Replay LLM to ensure determinism.
- **Contract tests** for API request/response models to avoid UI breakage.
- **Performance smoke**: time-boxed E2E run with small inputs to catch regressions.

---

## Observability
- **Logging**: `structlog` with JSON output and correlation IDs per run. Minimal PII by default.
- **Tracing**: OpenTelemetry spans per node; trace ID included in API responses.
- **Metrics**: Node durations, token counts, search calls, retrieval quality counters.

---

## Security and Governance
- **LLM**: Run locally via Ollama by default [[memory:5990558]].
- **Web Search**: Implement allowlisting and rate-limiting per `WebSearch-RateLimiting-Allowlisting.md`.
- **Data Handling**: Avoid sending secrets/content to external services in Phase 1. Mask logs.

---

## Rollout Plan (Phased)

### Phase 1: Minimal E2E Graph (this doc)
- Implement core nodes and linear edges.
- Expose basic API endpoints; return final report and citations.
- Persist artifacts to Qdrant and local disk.

### Phase 2: Feedback Loops and Memory
- Add planner-iterate loop when gaps detected.
- Integrate vector memory retrieval pre-search.
- Add evaluation harness for draft quality and citation coverage.

### Phase 3: Fact-Check and Advanced Tools
- Add fact-check node with verification prompts and cross-source checks.
- Add structured citation manager and consistency scoring.

### Phase 4: Advanced Microsoft Integrations
- If needed, integrate Office/Graph features, respecting project preferences to favor native functionality and small PRs.

Each phase should follow the user's Implementation Process (15–30 minute steps, test each step, small PRs).

---

## Risks and Mitigations
- **Model variability**: Use constrained prompts, JSON schema outputs, and retry policies.
- **Search noisiness**: Enforce allowlist, dedup aggressively, and use retrieval scoring.
- **State explosion**: Use checkpointing and prune intermediate artifacts by policy.
- **Parity drift**: Maintain contract tests; document deltas between Python and .NET flows.

---

## Minimal Code Sketch (Illustrative)
```python
from langgraph.graph import StateGraph
from pydantic import BaseModel

class GraphState(BaseModel):
    objective: str
    plan: dict | None = None
    results: list[dict] = []
    sections: list[dict] = []
    final_report: dict | None = None

def task_analyzer(state: GraphState) -> GraphState:
    # ... call LLM via Ollama adapter, return updated state
    return state

def planner(state: GraphState) -> GraphState:
    return state

def web_search(state: GraphState) -> GraphState:
    return state

def synthesize(state: GraphState) -> GraphState:
    return state

def finalize(state: GraphState) -> GraphState:
    return state

graph = StateGraph(GraphState)
graph.add_node("task_analyzer", task_analyzer)
graph.add_node("planner", planner)
graph.add_node("web_search", web_search)
graph.add_node("synthesize", synthesize)
graph.add_node("finalize", finalize)
graph.add_edge("task_analyzer", "planner")
graph.add_edge("planner", "web_search")
graph.add_edge("web_search", "synthesize")
graph.add_edge("synthesize", "finalize")
graph.set_entry_point("task_analyzer")
app = graph.compile()
```

---

## How to Test (Phase 1)
- Start Ollama and Qdrant locally.
- Run the Python API with `uvicorn`.
- `POST` a basic task to `/api/ran/graph/run` and confirm a final artifact is returned.
- Inspect Qdrant collections for stored chunks and artifacts.
- Review logs/traces to verify node execution order and durations.

---

## Next Steps Checklist
- Create `python/` scaffold with `requirements.txt` and FastAPI entry.
- Implement node stubs and compile the initial graph.
- Add unit tests for nodes and an E2E test of the minimal flow.
- Wire UI to call the new Python endpoints for a pilot path.





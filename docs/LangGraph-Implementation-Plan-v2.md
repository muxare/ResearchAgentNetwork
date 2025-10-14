## Python + LangGraph Implementation Plan v2 for ResearchAgentNetwork

This document provides an enhanced implementation plan for migrating ResearchAgentNetwork from .NET/Semantic Kernel to Python/LangGraph. This version improves upon the initial plan with better architecture patterns, modern LangGraph features, clearer parity mapping, and production-ready considerations.

### Document Improvements Over v1
- **Enhanced graph architecture** with parallel execution, conditional routing, and sub-graphs
- **Full feature parity mapping** to existing .NET agents and orchestration patterns
- **Modern LangGraph features** including streaming, checkpointing, and human-in-the-loop
- **Production deployment** with Docker, scaling, and monitoring
- **Concrete code examples** for each agent node
- **Migration strategy** for gradual transition from .NET to Python
- **LangSmith integration** for debugging and observability

---

## Goals and Scope

### Primary Goals
1. **Feature Parity**: Replicate all core .NET functionality (decomposition, execution, quality assessment, aggregation, finalization)
2. **Performance**: Match or exceed .NET orchestrator throughput and concurrency
3. **Maintainability**: Leverage LangGraph's built-in features for simpler code
4. **Observability**: Better tracing and debugging via LangSmith
5. **Gradual Migration**: Run alongside .NET system; migrate endpoints incrementally

### Scope by Phase

**Phase 1 (Weeks 1-2): Core Agent Parity**
- Implement all existing agent types (TaskAnalyzer, Executor, QualityAssessment, Aggregator, etc.)
- Basic graph orchestration with decomposition and aggregation
- Ollama LLM integration with structured outputs
- API endpoints matching .NET interface

**Phase 2 (Weeks 3-4): Advanced Features**
- Vector memory integration with Qdrant
- Task merging and deduplication
- Web search integration with rate limiting
- Finalization pipeline (ReportOutline, SectionWriter, FactCheck, CitationManager)

**Phase 3 (Weeks 5-6): Production Readiness**
- Streaming responses and SSE
- Persistent checkpointing for long-running tasks
- Human-in-the-loop for quality gates
- Docker deployment and scaling

**Phase 4 (Weeks 7-8): Migration and Optimization**
- Database migration tools
- A/B testing framework
- Performance optimization
- Full .NET feature parity validation

---

## Architecture

### Component Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     Frontend (Unchanged)                     │
│              SvelteKit / React Router UI                     │
└─────────────────────────────────────────────────────────────┘
                              │
                              ├─── HTTP/SSE ────┐
                              │                  │
                              ▼                  ▼
┌─────────────────────────────────┐  ┌──────────────────────────┐
│   .NET API (Legacy/Gradual)     │  │  Python API (FastAPI)    │
│   ResearchAgentNetwork.Web      │  │  Port 8090               │
└─────────────────────────────────┘  └──────────────────────────┘
                                                 │
                                                 ▼
                              ┌──────────────────────────────────┐
                              │   LangGraph Orchestrator         │
                              │   (Main Graph + Sub-Graphs)      │
                              └──────────────────────────────────┘
                                     │        │        │
                   ┌─────────────────┼────────┼────────┼─────────────────┐
                   ▼                 ▼        ▼        ▼                 ▼
           ┌──────────────┐  ┌──────────┐  ┌────────┐  ┌────────────┐  ┌────────┐
           │    Ollama    │  │  Qdrant  │  │  DB    │  │  Tavily    │  │  File  │
           │  (LLM)       │  │ (Vector) │  │ (SQL)  │  │ (Search)   │  │ Store  │
           └──────────────┘  └──────────┘  └────────┘  └────────────┘  └────────┘
```

### LangGraph Architecture Patterns

Unlike the linear graph in v1, we'll use **hierarchical sub-graphs** and **conditional routing**:

1. **Main Orchestrator Graph**: Handles task lifecycle, decomposition tree, and result aggregation
2. **Execution Sub-Graph**: Per-task execution with analyzer → executor → quality assessment
3. **Finalization Sub-Graph**: Report generation pipeline (outline → sections → fact-check → citations)
4. **Merge Sub-Graph**: Task deduplication and similarity merging

This mirrors the .NET architecture where `ResearchOrchestrator` manages queues and delegates to specialized agents.

---

## .NET to LangGraph Parity Mapping

### Agent Mapping

| .NET Agent | LangGraph Node(s) | Purpose | Key Differences |
|------------|------------------|---------|-----------------|
| `TaskAnalyzerAgent` | `task_analyzer_node` | Analyze complexity, decide decomposition | LangGraph can use conditional edges vs. boolean return |
| `TaskMergerAgent` | `merger_node` + `similarity_check_node` | Deduplicate/merge similar tasks | LangGraph can parallelize similarity checks |
| `ExecutorAgent` | `executor_node` | Execute atomic tasks, gather sources | Similar, but can stream partial results |
| `QualityAssessmentAgent` | `quality_assessment_node` | Evaluate quality, generate follow-ups | Can use sub-graph for iterative improvement |
| `AggregatorAgent` | `aggregator_node` | Synthesize child results into parent | LangGraph built-in reducers simplify this |
| `ReportOutlineAgent` | `outline_node` | Generate report structure | Part of finalization sub-graph |
| `SectionWriterAgent` | `section_writer_node` (parallel) | Write individual sections | LangGraph can parallelize section writing |
| `FactCheckAgent` | `fact_check_node` | Validate claims | Can use tools for external verification |
| `CitationManagerAgent` | `citation_node` | Format citations | Simpler with Pydantic models |
| `WebSearchAgent` | `web_search_node` + `tavily_tool` | Web search with rate limiting | Use LangChain's Tavily integration |

### Orchestration Patterns

| .NET Pattern | LangGraph Equivalent | Implementation |
|--------------|---------------------|----------------|
| Task Queue (`ConcurrentQueue`) | Graph with parallel node execution | Use `add_edge()` with fan-out/fan-in |
| Semaphore throttling (`MaxConcurrency`) | Thread pool executor with semaphore | Configure `thread_pool_executor` |
| Event broadcasting (`TaskEvent`) | State updates + streaming | Use `stream_mode="values"` + SSE |
| Task registry (`Dictionary`) | Graph state + checkpointer | Use `MemorySaver` or `PostgresSaver` |
| Retry logic | Built-in LangGraph retry | Use `@retry` decorator or error edges |
| Priority queue | Custom node with prioritization | Implement priority scheduler node |

### Key Architecture Differences

**Advantages of LangGraph:**
- **Built-in streaming**: No custom SSE implementation needed
- **Persistent checkpointing**: Resume from any node on failure
- **Visualization**: LangGraph Studio shows execution flow
- **Time travel debugging**: Replay from any checkpoint
- **Human-in-the-loop**: Built-in interrupts for approval gates

**Challenges:**
- **Concurrency model**: LangGraph is more functional; need to adapt imperative queue logic
- **State management**: LangGraph uses immutable state; .NET uses mutable task objects
- **Event system**: LangGraph streaming vs. custom event publisher

---

## Enhanced Graph Structure

### Main Orchestrator Graph

```python
from langgraph.graph import StateGraph, END
from langgraph.checkpoint.memory import MemorySaver
from typing import Annotated, Literal
from pydantic import BaseModel

class OrchestratorState(BaseModel):
    """Main orchestrator state matching .NET ResearchTask"""
    task_id: str
    description: str
    parent_id: str | None = None
    status: Literal["Pending", "Processing", "Completed", "Failed", "Cancelled"]
    priority: int = 5
    depth: int = 0

    # Analysis results
    complexity_analysis: dict | None = None
    should_decompose: bool = False
    subtasks: list[str] = []  # Child task IDs

    # Execution results
    result: dict | None = None
    quality_assessment: dict | None = None
    follow_up_tasks: list[str] = []

    # Aggregation
    child_results: list[dict] = []
    aggregated_result: dict | None = None

    # Finalization
    final_report: dict | None = None

    # Metadata
    metadata: dict = {}
    retry_count: int = 0
    errors: list[str] = []

def route_after_analysis(state: OrchestratorState) -> Literal["decompose", "execute", "merge_check"]:
    """Conditional routing after task analysis"""
    if state.retry_count > 0:
        return "merge_check"  # Check for duplicate work on retry
    elif state.should_decompose and state.depth < MAX_DEPTH:
        return "decompose"
    else:
        return "execute"

def route_after_quality(state: OrchestratorState) -> Literal["generate_followups", "aggregate", "end"]:
    """Route based on quality assessment"""
    qa = state.quality_assessment or {}
    if qa.get("confidence", 1.0) < 0.6 and state.retry_count < MAX_RETRIES:
        return "generate_followups"
    elif state.parent_id and all_siblings_complete(state):
        return "aggregate"
    else:
        return "end"

# Build main graph
workflow = StateGraph(OrchestratorState)

# Add nodes
workflow.add_node("analyze", task_analyzer_node)
workflow.add_node("merge_check", merger_node)
workflow.add_node("decompose", decompose_node)
workflow.add_node("execute", executor_node)
workflow.add_node("quality_check", quality_assessment_node)
workflow.add_node("generate_followups", followup_generator_node)
workflow.add_node("aggregate", aggregator_node)
workflow.add_node("finalize", finalization_subgraph)

# Add edges
workflow.set_entry_point("analyze")
workflow.add_conditional_edges(
    "analyze",
    route_after_analysis,
    {
        "decompose": "decompose",
        "execute": "execute",
        "merge_check": "merge_check"
    }
)
workflow.add_edge("merge_check", "execute")
workflow.add_edge("decompose", END)  # Subtasks processed separately
workflow.add_edge("execute", "quality_check")
workflow.add_conditional_edges(
    "quality_check",
    route_after_quality,
    {
        "generate_followups": "generate_followups",
        "aggregate": "aggregate",
        "end": END
    }
)
workflow.add_edge("generate_followups", END)
workflow.add_edge("aggregate", "finalize")
workflow.add_edge("finalize", END)

# Compile with checkpointing
memory = MemorySaver()
app = workflow.compile(checkpointer=memory)
```

### Parallel Execution Pattern

For section writing (matching .NET's parallel execution):

```python
from langgraph.graph import StateGraph
from concurrent.futures import ThreadPoolExecutor
import asyncio

class FinalizationState(BaseModel):
    outline: dict
    sections: dict[str, str] = {}  # section_id -> content
    citations: list[dict] = []
    final_markdown: str = ""

async def write_section_node(state: FinalizationState, section_id: str) -> dict:
    """Write a single section - called in parallel"""
    section_def = state.outline["sections"][section_id]
    # Call LLM to write section
    content = await section_writer_agent.write(section_def, state.citations)
    return {section_id: content}

async def parallel_section_writer_node(state: FinalizationState) -> FinalizationState:
    """Orchestrate parallel section writing"""
    section_ids = list(state.outline["sections"].keys())

    # Execute all sections in parallel
    async with asyncio.TaskGroup() as tg:
        tasks = [
            tg.create_task(write_section_node(state, sid))
            for sid in section_ids
        ]

    # Merge results
    for task in tasks:
        state.sections.update(task.result())

    return state

# Add to finalization sub-graph
finalization_graph = StateGraph(FinalizationState)
finalization_graph.add_node("outline", outline_node)
finalization_graph.add_node("write_sections", parallel_section_writer_node)
finalization_graph.add_node("fact_check", fact_check_node)
finalization_graph.add_node("citations", citation_manager_node)
finalization_graph.add_node("render", markdown_renderer_node)
```

---

## Structured Outputs and Tool Calling

### Using LangChain's `with_structured_output()`

Replaces .NET's `KernelExtensions.GetStructuredResponseAsync<T>()`:

```python
from langchain_core.pydantic_v1 import BaseModel, Field
from langchain_ollama import ChatOllama

class ComplexityAnalysis(BaseModel):
    """Schema for task complexity analysis"""
    complexity_level: Literal["Simple", "Moderate", "Complex"] = Field(
        description="Overall complexity level"
    )
    requires_decomposition: bool = Field(
        description="Whether task should be broken down"
    )
    estimated_subtasks: int = Field(
        description="Estimated number of subtasks if decomposed"
    )
    reasoning: str = Field(
        description="Explanation of complexity assessment"
    )
    subtask_descriptions: list[str] = Field(
        default=[],
        description="Suggested subtask descriptions if decomposition needed"
    )

class TaskAnalyzerAgent:
    def __init__(self, model: str = "llama3.1"):
        self.llm = ChatOllama(model=model, temperature=0)
        self.structured_llm = self.llm.with_structured_output(ComplexityAnalysis)

    async def analyze(self, task_description: str) -> ComplexityAnalysis:
        """Analyze task complexity with guaranteed structured output"""
        prompt = f"""Analyze the following research task and determine its complexity:

Task: {task_description}

Consider:
- Number of distinct questions or topics
- Depth of research required
- Interdependencies between components
- Estimated time and effort

Provide a structured analysis."""

        return await self.structured_llm.ainvoke(prompt)

# Usage in node
async def task_analyzer_node(state: OrchestratorState) -> OrchestratorState:
    agent = TaskAnalyzerAgent()
    analysis = await agent.analyze(state.description)

    state.complexity_analysis = analysis.dict()
    state.should_decompose = analysis.requires_decomposition

    # Generate subtasks if needed
    if analysis.requires_decomposition:
        state.subtasks = await create_subtasks(
            state.task_id,
            analysis.subtask_descriptions,
            state.depth + 1
        )

    return state
```

### Tool Calling for Web Search

```python
from langchain_community.tools.tavily_search import TavilySearchResults
from langchain.agents import AgentExecutor, create_tool_calling_agent
from langchain_core.prompts import ChatPromptTemplate

class WebSearchAgent:
    def __init__(self):
        self.llm = ChatOllama(model="llama3.1", temperature=0)
        self.search_tool = TavilySearchResults(
            max_results=5,
            search_depth="advanced",
            include_answer=True,
            include_raw_content=True
        )

        # Create agent with tool calling
        prompt = ChatPromptTemplate.from_messages([
            ("system", "You are a research assistant. Use the search tool to find relevant information."),
            ("human", "{input}"),
            ("placeholder", "{agent_scratchpad}"),
        ])

        self.agent = create_tool_calling_agent(
            self.llm,
            [self.search_tool],
            prompt
        )
        self.executor = AgentExecutor(agent=self.agent, tools=[self.search_tool])

    async def search(self, query: str) -> list[dict]:
        """Execute web search with automatic query refinement"""
        result = await self.executor.ainvoke({"input": query})
        return result["output"]
```

---

## Streaming and Real-Time Updates

### Server-Sent Events (SSE) with Streaming

LangGraph's built-in streaming replaces custom .NET event publisher:

```python
from fastapi import FastAPI
from fastapi.responses import StreamingResponse
from langgraph.graph import StateGraph
import json

app = FastAPI()

@app.post("/api/tasks")
async def submit_task(task: TaskRequest):
    """Submit task and return task_id"""
    task_id = str(uuid.uuid4())
    initial_state = OrchestratorState(
        task_id=task_id,
        description=task.description,
        status="Pending"
    )

    # Store initial state
    await save_task(initial_state)

    # Start graph execution in background
    asyncio.create_task(execute_graph(task_id, initial_state))

    return {"task_id": task_id, "status": "Pending"}

@app.get("/api/tasks/{task_id}/stream")
async def stream_task_updates(task_id: str):
    """Stream real-time task updates via SSE"""

    async def event_generator():
        config = {"configurable": {"thread_id": task_id}}

        # Stream graph execution
        async for event in app.astream_events(
            state,
            config=config,
            version="v1"
        ):
            # Convert LangGraph events to .NET TaskEvent format
            task_event = {
                "taskId": task_id,
                "timestamp": datetime.utcnow().isoformat(),
                "eventType": map_event_type(event["event"]),
                "description": event.get("data", {}).get("output", ""),
                "metadata": event.get("metadata", {})
            }

            yield f"data: {json.dumps(task_event)}\n\n"

            # Break if graph completed
            if event["event"] == "on_chain_end":
                break

    return StreamingResponse(
        event_generator(),
        media_type="text/event-stream"
    )

def map_event_type(langgraph_event: str) -> str:
    """Map LangGraph events to .NET TaskEvent types"""
    mapping = {
        "on_chain_start": "TaskStarted",
        "on_chain_end": "TaskCompleted",
        "on_chain_error": "TaskFailed",
        "on_tool_start": "ToolExecutionStarted",
        "on_tool_end": "ToolExecutionCompleted",
    }
    return mapping.get(langgraph_event, "TaskUpdated")
```

### Streaming with Checkpoints

Resume from failures with persistent checkpoints:

```python
from langgraph.checkpoint.postgres import PostgresSaver

# Use PostgreSQL for production checkpointing
connection_string = "postgresql://user:pass@localhost/randb"
checkpointer = PostgresSaver.from_conn_string(connection_string)

app = workflow.compile(checkpointer=checkpointer)

# Resume from checkpoint after failure
async def resume_task(task_id: str):
    """Resume task from last successful checkpoint"""
    config = {"configurable": {"thread_id": task_id}}

    # Get last checkpoint
    checkpoint = await checkpointer.aget(config)

    if checkpoint:
        # Resume from checkpoint
        result = await app.ainvoke(None, config=config)
        return result
    else:
        raise ValueError(f"No checkpoint found for task {task_id}")
```

---

## Enhanced Testing Strategy

### Unit Tests with Mocked LLM

```python
import pytest
from unittest.mock import AsyncMock, Mock
from langgraph.graph import StateGraph

@pytest.fixture
def mock_llm():
    """Mock LLM responses for deterministic testing"""
    mock = AsyncMock()
    mock.ainvoke.return_value = ComplexityAnalysis(
        complexity_level="Complex",
        requires_decomposition=True,
        estimated_subtasks=3,
        reasoning="Multi-faceted research question",
        subtask_descriptions=[
            "Research quantum algorithms",
            "Analyze cryptographic vulnerabilities",
            "Evaluate migration strategies"
        ]
    )
    return mock

@pytest.mark.asyncio
async def test_task_analyzer_node(mock_llm):
    """Test task analyzer with mocked LLM"""
    state = OrchestratorState(
        task_id="test-1",
        description="Analyze quantum computing impact on cryptography"
    )

    with patch('ran_py.llm.ollama_provider.ChatOllama', return_value=mock_llm):
        result = await task_analyzer_node(state)

    assert result.should_decompose is True
    assert len(result.subtasks) == 3
    assert result.complexity_analysis["complexity_level"] == "Complex"
```

### Integration Tests with Replay

Use LangSmith's replay feature for deterministic integration tests:

```python
from langsmith import Client
from langsmith.run_helpers import traceable

@traceable(run_type="chain")
async def test_full_graph_execution():
    """Integration test with LangSmith tracing"""
    initial_state = OrchestratorState(
        task_id="integration-test-1",
        description="Test task"
    )

    config = {"configurable": {"thread_id": "test-thread"}}
    result = await app.ainvoke(initial_state, config=config)

    assert result.status == "Completed"
    assert result.final_report is not None

# Replay from LangSmith trace
async def test_replay_from_trace():
    """Replay execution from saved LangSmith trace"""
    client = Client()

    # Get trace from production run
    trace = client.read_run("original-run-id")

    # Replay with same inputs
    result = await app.ainvoke(
        trace.inputs,
        config={"configurable": {"thread_id": "replay-thread"}}
    )

    # Assert deterministic behavior
    assert result.status == trace.outputs["status"]
```

### Contract Tests for API Parity

Ensure Python API matches .NET API contract:

```python
import httpx
import pytest

@pytest.mark.parametrize("endpoint,method,expected_schema", [
    ("/api/tasks", "POST", TaskResponse),
    ("/api/tasks/{id}", "GET", TaskDetail),
    ("/api/tasks/{id}/children", "GET", list[TaskDetail]),
])
async def test_api_contract(endpoint, method, expected_schema):
    """Verify API response schemas match .NET endpoints"""
    async with httpx.AsyncClient(base_url="http://localhost:8090") as client:
        if method == "POST":
            response = await client.post(endpoint, json={"description": "test"})
        else:
            response = await client.get(endpoint.replace("{id}", "test-id"))

        assert response.status_code in [200, 201]

        # Validate schema
        data = response.json()
        validated = expected_schema(**data)
        assert validated is not None
```

---

## Database Integration and Migration

### Shared Database Access

Both .NET and Python services access the same database:

```python
from sqlalchemy.ext.asyncio import create_async_engine, AsyncSession
from sqlalchemy.orm import declarative_base, sessionmaker

# Match .NET EF Core entities
Base = declarative_base()

class TaskEntity(Base):
    """Matches ResearchAgentNetwork.Persistence.Entities.TaskEntity"""
    __tablename__ = "Tasks"

    Id = Column(String, primary_key=True)
    Description = Column(String, nullable=False)
    Status = Column(String, nullable=False)
    ParentId = Column(String, nullable=True)
    CreatedAtUtc = Column(DateTime, nullable=False)
    UpdatedAtUtc = Column(DateTime, nullable=False)
    Category = Column(String, nullable=False)
    Priority = Column(Integer, nullable=False)
    Depth = Column(Integer, nullable=False)
    Metadata = Column(JSON, nullable=True)

class TaskRepository:
    """Repository matching .NET ITaskRepository interface"""

    def __init__(self, session: AsyncSession):
        self.session = session

    async def create(self, task: TaskEntity) -> TaskEntity:
        self.session.add(task)
        await self.session.commit()
        await self.session.refresh(task)
        return task

    async def get_by_id(self, task_id: str) -> TaskEntity | None:
        result = await self.session.execute(
            select(TaskEntity).where(TaskEntity.Id == task_id)
        )
        return result.scalar_one_or_none()

    async def update(self, task: TaskEntity) -> TaskEntity:
        task.UpdatedAtUtc = datetime.utcnow()
        await self.session.commit()
        await self.session.refresh(task)
        return task
```

### Migration Strategy

Gradual migration with feature flags:

```python
from typing import Literal

class FeatureFlags:
    """Control which system handles which features"""

    def __init__(self):
        self.flags = {
            "use_python_analyzer": os.getenv("USE_PYTHON_ANALYZER", "false") == "true",
            "use_python_executor": os.getenv("USE_PYTHON_EXECUTOR", "false") == "true",
            "use_python_aggregator": os.getenv("USE_PYTHON_AGGREGATOR", "false") == "true",
        }

    def should_use_python(self, component: str) -> bool:
        return self.flags.get(f"use_python_{component}", False)

# In .NET API, proxy to Python when flag enabled
@app.MapPost("/api/tasks", async (TaskRequest request) =>
{
    if (featureFlags.ShouldUsePython("analyzer"))
    {
        // Proxy to Python service
        var response = await httpClient.PostAsJsonAsync(
            "http://localhost:8090/api/tasks",
            request
        );
        return await response.Content.ReadFromJsonAsync<TaskResponse>();
    }
    else
    {
        // Use .NET orchestrator
        return await orchestrator.SubmitResearchTask(request);
    }
});
```

---

## Deployment and Scaling

### Docker Compose for Development

```yaml
# docker-compose.yml
version: '3.8'

services:
  ran-python:
    build:
      context: ./python
      dockerfile: Dockerfile
    ports:
      - "8090:8090"
    environment:
      - OLLAMA_BASE_URL=http://ollama:11434
      - QDRANT_URL=http://qdrant:6333
      - DATABASE_URL=postgresql://postgres:password@postgres:5432/randb
      - LANGSMITH_API_KEY=${LANGSMITH_API_KEY}
      - LANGSMITH_TRACING=true
    depends_on:
      - ollama
      - qdrant
      - postgres
    volumes:
      - ./python/ran_py:/app/ran_py  # Hot reload
    command: uvicorn ran_py.api.app:app --host 0.0.0.0 --port 8090 --reload

  ollama:
    image: ollama/ollama:latest
    ports:
      - "11434:11434"
    volumes:
      - ollama-data:/root/.ollama
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]

  qdrant:
    image: qdrant/qdrant:latest
    ports:
      - "6333:6333"
      - "6334:6334"
    volumes:
      - qdrant-data:/qdrant/storage

  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: randb
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: password
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data

  ran-dotnet:
    build:
      context: .
      dockerfile: ResearchAgentNetwork.Web/Dockerfile
    ports:
      - "5000:8080"
    environment:
      - AI_PROVIDER=Ollama
      - Ollama__Endpoint=http://ollama:11434
      - Database__Provider=Sqlite
    depends_on:
      - ollama

volumes:
  ollama-data:
  qdrant-data:
  postgres-data:
```

### Python Dockerfile

```dockerfile
# python/Dockerfile
FROM python:3.11-slim

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y \
    gcc \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements and install dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY ran_py/ ./ran_py/

# Expose port
EXPOSE 8090

# Run with production server
CMD ["uvicorn", "ran_py.api.app:app", "--host", "0.0.0.0", "--port", "8090"]
```

### Horizontal Scaling with Load Balancer

```yaml
# docker-compose.scale.yml
services:
  ran-python:
    deploy:
      replicas: 3
    environment:
      - WORKER_ID=${HOSTNAME}

  nginx:
    image: nginx:alpine
    ports:
      - "8090:80"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
    depends_on:
      - ran-python

# nginx.conf
upstream ran_python_backend {
    least_conn;
    server ran-python-1:8090;
    server ran-python-2:8090;
    server ran-python-3:8090;
}

server {
    listen 80;

    location / {
        proxy_pass http://ran_python_backend;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
    }
}
```

---

## LangSmith Integration

### Setup and Configuration

```python
import os
from langsmith import Client
from langsmith.run_helpers import traceable

# Enable LangSmith tracing
os.environ["LANGSMITH_API_KEY"] = "your-api-key"
os.environ["LANGSMITH_TRACING"] = "true"
os.environ["LANGSMITH_PROJECT"] = "research-agent-network"

client = Client()

@traceable(run_type="chain", name="research_orchestrator")
async def execute_graph(task_id: str, state: OrchestratorState):
    """Traced graph execution"""
    config = {
        "configurable": {"thread_id": task_id},
        "metadata": {
            "task_id": task_id,
            "user_id": state.metadata.get("user_id"),
            "environment": "production"
        }
    }

    result = await app.ainvoke(state, config=config)
    return result

# Add custom evaluators
from langsmith.evaluation import evaluate, LangChainStringEvaluator

def quality_evaluator(run, example):
    """Evaluate research quality"""
    output = run.outputs["final_report"]

    # Check for citations
    has_citations = len(output.get("citations", [])) > 0

    # Check for completeness
    required_sections = ["introduction", "findings", "conclusion"]
    has_sections = all(
        section in output.get("sections", {})
        for section in required_sections
    )

    score = (has_citations + has_sections) / 2

    return {"key": "quality_score", "score": score}

# Run evaluation
results = evaluate(
    lambda inputs: execute_graph(inputs["task_id"], inputs["state"]),
    data="research_tasks_dataset",
    evaluators=[quality_evaluator],
)
```

### Debugging with LangSmith

```python
# View trace in LangSmith UI
trace_url = f"https://smith.langchain.com/o/{org_id}/projects/p/{project_id}/r/{run_id}"

# Programmatic trace analysis
def analyze_trace(run_id: str):
    """Analyze execution trace for performance issues"""
    run = client.read_run(run_id)

    # Find slowest nodes
    node_durations = {}
    for child in run.child_runs:
        node_name = child.name
        duration = (child.end_time - child.start_time).total_seconds()
        node_durations[node_name] = duration

    slowest = sorted(node_durations.items(), key=lambda x: x[1], reverse=True)[:3]

    print("Slowest nodes:")
    for node, duration in slowest:
        print(f"  {node}: {duration:.2f}s")

    # Check for errors
    if run.error:
        print(f"Error: {run.error}")
        print(f"Stack trace: {run.stack_trace}")
```

---

## Production Readiness Checklist

### Phase 3 Deliverables

**Reliability:**
- [ ] Persistent checkpointing with PostgreSQL
- [ ] Automatic retry with exponential backoff
- [ ] Circuit breaker for external services (Tavily, Qdrant)
- [ ] Graceful degradation when optional services unavailable
- [ ] Health check endpoints (`/health`, `/health/ready`, `/health/live`)

**Observability:**
- [ ] Structured logging with correlation IDs
- [ ] LangSmith tracing for all executions
- [ ] Prometheus metrics export
- [ ] OpenTelemetry integration
- [ ] Error tracking (Sentry/Rollbar)

**Security:**
- [ ] API authentication (JWT matching .NET)
- [ ] Rate limiting per user/API key
- [ ] Input validation with Pydantic
- [ ] Secrets management (no hardcoded keys)
- [ ] CORS configuration

**Performance:**
- [ ] Async/await throughout
- [ ] Connection pooling (DB, HTTP)
- [ ] Caching for frequently accessed data
- [ ] Horizontal scaling tested
- [ ] Load testing completed (target: 100 concurrent tasks)

**Operations:**
- [ ] Docker images published
- [ ] Kubernetes manifests ready
- [ ] CI/CD pipeline configured
- [ ] Automated database migrations
- [ ] Rollback procedure documented

---

## Migration Rollout Plan

### Week 1-2: Foundation
1. Set up Python project structure
2. Implement core Pydantic models matching .NET entities
3. Create basic FastAPI app with health endpoints
4. Set up LangSmith and logging
5. Implement Ollama provider wrapper
6. Write first agent node (TaskAnalyzer) with tests

### Week 3-4: Core Graph
1. Implement all agent nodes
2. Build main orchestrator graph
3. Add checkpointing and streaming
4. Implement database repositories
5. Create API endpoints matching .NET interface
6. Integration tests for full graph execution

### Week 5-6: Advanced Features
1. Vector memory integration
2. Web search with rate limiting
3. Finalization sub-graph
4. Human-in-the-loop gates
5. Performance optimization
6. Load testing

### Week 7-8: Migration and Production
1. Deploy alongside .NET service
2. Feature flag system
3. A/B testing framework
4. Gradual traffic shift (10% → 50% → 100%)
5. Monitoring and alerting
6. Documentation and runbooks

### Success Criteria
- All .NET features replicated
- API contract compatibility
- Performance parity or better
- 99.9% uptime during migration
- Zero data loss
- Rollback capability at each step

---

## Conclusion

This enhanced plan provides a comprehensive roadmap for migrating to LangGraph while maintaining production stability. Key improvements over v1:

1. **Full feature parity mapping** to existing .NET system
2. **Production-grade architecture** with scaling, monitoring, and reliability
3. **Modern LangGraph features** (streaming, checkpointing, LangSmith)
4. **Concrete code examples** for each component
5. **Clear migration strategy** with gradual rollout
6. **Comprehensive testing** approach

The phased approach allows incremental validation and risk mitigation while delivering value at each milestone.

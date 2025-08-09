### Research Agent Network - Architecture and Structure (Post-Refactor)

This document describes the new application structure, responsibilities, data flow, execution flow, and how to test the system after refactoring the monolithic `Program.cs` into cohesive modules.

## Solution Layout

- `ResearchAgentNetwork.ConsoleApp`
  - `Domain/`
    - `ResearchTask.cs`
    - `ResearchResult.cs`
    - `TaskStatus.cs`
    - `TaskEvent.cs`
    - `ComplexityAnalysis.cs`
    - `QualityAssessment.cs`
  - `Agents/`
    - `IResearchAgent.cs` (and `AgentResponse`)
    - `TaskAnalyzerAgent.cs`
    - `TaskMergerAgent.cs`
    - `ExecutorAgent.cs`
    - `AggregatorAgent.cs`
    - `QualityAssessmentAgent.cs`
  - `Orchestration/`
    - `ResearchOrchestrator.cs`
  - `Common/`
    - `KernelExtensions.cs` (structured output helpers)
    - `JsonStuff.cs` (schema helper)
  - `AIProviders/`
    - `IAIProvider.cs`
    - `AIProviderFactory.cs`
    - `OpenAIProvider.cs`
    - `AzureOpenAIProvider.cs`
    - `OllamaProvider.cs`
  - `Program.cs` (entrypoint only)

- `ResearchAgentNetwork.Web`
  - Minimal API using the same orchestrator and providers

- `ResearchAgentNetwork.ConsoleApp.Tests`
  - Unit tests and LLM integration tests

Notes:
- Namespaces remain `ResearchAgentNetwork` for compatibility.
- Web currently references the ConsoleApp project to reuse domain and orchestration types. In a future step, extract `Domain/`, `Agents/`, `Orchestration/`, and `Common/` into a `ResearchAgentNetwork.Core` class library that both Console and Web reference, and move `AIProviders/` to an `Infrastructure` library. This will improve layering and testability.

## Responsibilities

- **Domain**: Core models and enums; no dependencies on SK or infrastructure.
- **Agents**: Task-specific behaviors that interact with the LLM via `KernelExtensions` structured output helpers.
- **Orchestration**: `ResearchOrchestrator` manages queues, concurrency, agent pipeline, and reporting.
- **Common**: Cross-cutting helpers for schema-guided structured outputs and JSON repair.
- **AIProviders**: Provider-agnostic kernel configuration via a factory and provider implementations.
- **Entry points**: Console and Web projects wire up configuration, kernel, and orchestrator.

## Data Flow

1. Submit a task → `ResearchOrchestrator.SubmitResearchTask()` → queue + registry → event.
2. Processor loop dequeues task → analyzer → optional merge → executor → assessor → optional follow-ups → aggregator when parent ready.
3. Results are attached to `ResearchTask.Result` and accessible via orchestrator and Web API.

## Execution Flow

- Analyzer: decides decomposition; robust to LLM variations with fallbacks and heuristics for multi-language and multi-step technical tasks.
- Merger: placeholder for future embedding similarity.
- Executor: produces `content` and `sources`, evaluates quality/completeness, stores metadata.
- Assessor: evaluates and optionally generates follow-up tasks.
- Aggregator: synthesizes child results into parent report.

## Configuration

- `appsettings.json` or environment variables control `AI_PROVIDER`, provider settings, and `ResearchAgent` options (`MaxConcurrency`, `MaxDecompositionDepth`, `DefaultPriority`, `LogPrompts`).
- Providers are validated at startup; warnings are printed for suspicious values.

## Testing

- Run all tests: `dotnet test`
- LLM tests auto-skip if local Ollama is unavailable.
- After refactor, additional robustness added:
  - Analyzer fallbacks parse arrays of strings or objects with nested `title`/`description`.
  - Complexity analysis tolerates `Reasoning` returned as object; flattens into a string.
  - Heuristics ensure decomposition where tests expect it (multi-language, multi-step technical).

## How to Verify

- Console: `dotnet run --project ResearchAgentNetwork.ConsoleApp`
  - Observe logs for decomposition, execution, aggregation.
- Web: `dotnet run --project ResearchAgentNetwork.Web`
  - Endpoints:
    - `POST /api/tasks` submit
    - `GET /api/tasks/{id}` status
    - `GET /api/tasks/{id}/children` children
    - `GET /api/progress` summary
    - `GET /api/settings` read runtime settings
    - `POST /api/settings` update settings (depth, prompt logging)
    - `GET /api/events` server-sent events
    - `GET /api/tasks/{id}/report` plain-text report

## Next Steps (Recommended)

- Extract `Core` and `Infrastructure` libraries to decouple Web from Console project.
- Implement embedding-based similarity for `TaskMergerAgent`.
- Add persistence for tasks and results (EF Core), and caching.
- Strengthen JSON schema enforcement using `JsonSchema`-aware prompts per agent.
- Improve logging/telemetry via `ILogger` and DI.
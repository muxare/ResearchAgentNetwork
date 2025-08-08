# Simple Research Assistant – Plan (LLM-only, Web Search Ready)

## Scope

- Build a minimal, reliable research assistant that uses LLM-only reasoning to: analyze, decompose, execute, assess, and aggregate research tasks.
- Keep clear extension points for later web search and embeddings-powered similarity, but don’t implement them now.

## Current State (Summary)

- Agents implemented: `TaskAnalyzerAgent`, `TaskMergerAgent` (stub similarity), `ExecutorAgent`, `AggregatorAgent`, `QualityAssessmentAgent`.
- Orchestration: `ResearchOrchestrator` with in-memory queue/registry and concurrency.
- Kernel helpers: `KernelExtensions.WithStructuredOutput*` for JSON schema guided outputs.
- Providers: `Ollama`, `OpenAI`, `AzureOpenAI` via `AIProviderFactory`.
- Tests: Analyzer simple tests and optional Ollama LLM integration tests.

## Gaps to Close for a Working Simple Assistant

- Inconsistent structured outputs: several agents parse raw JSON/dictionaries instead of using `WithStructuredOutput*`.
- Decomposition schema mismatch: asks for `ResearchTask[]` but parses a `List<string>`.
- Aggregation blocked: `AggregatorAgent` can’t access child results (no registry access).
- Result sources: `ExecutorAgent` doesn’t populate `ResearchResult.Sources` consistently.
- Docs: provider setup section is out-of-sync with factory-based configuration.

## Design Decisions

- Structured outputs everywhere: Use `WithStructuredOutputRetry<T>` across agents to enforce deterministic JSON.
- Decomposition output type: return `List<string>` (simpler, less brittle); convert to `ResearchTask` in code.
- Aggregation access: inject a dependency to fetch children from orchestrator (function/delegate) rather than giving the agent direct registry access.
- Sources handling: have the executor prompt return sources as an array in the structured output; accept that they’re LLM-claimed until web search lands.
- Web search readiness: define an interface now; wire in later without changing agent boundaries.

## Targeted Changes

### 1) Structured Output DTOs

- Atomicity check
  ```csharp
  public class AtomicityCheck { public bool IsAtomic { get; set; } public string Reasoning { get; set; } }
  ```
- Quality score
  ```csharp
  public class QualityScore { public double Score { get; set; } }
  ```
- Completeness (reuse existing class, ensure consistent casing in schema)
  ```csharp
  public class Completeness { public bool NeedsMoreResearch { get; set; } public string Reasoning { get; set; } }
  ```
- Executor result (aligns with `ResearchResult`)
  ```csharp
  public class ExecutorStructuredResult { public string Content { get; set; } public string[] Sources { get; set; } }
  ```

Use `kernel.WithStructuredOutputRetry<T>(prompt)` in all agents instead of manual deserialization.

### 2) Decomposition Schema and Mapping

- Change schema gen in `TaskAnalyzerAgent.DecomposeTask` to `GenerateJsonSchemaFromClass<string[]>()`.
- Prompt: return JSON array of subtask descriptions only.
- Map to `List<ResearchTask>` with `ParentTaskId` set in orchestrator.

### 3) Aggregation Access to Children

- Create an accessor dependency and inject into `AggregatorAgent`:
  ```csharp
  public delegate List<ResearchTask> GetChildren(Guid parentId);
  ```
- `AggregatorAgent` receives `GetChildren getChildren` via constructor and uses it in `GetSubTaskResults()`.
- Orchestrator passes `id => _taskRegistry.Values.Where(t => t.ParentTaskId == id).ToList()`.

### 4) Executor Output and Sources

- Adjust executor prompt to return structured fields `content`, `sources`.
- Deserialize to `ExecutorStructuredResult` and map to `ResearchResult`.
- Keep quality scoring and completeness checks as structured outputs (`QualityScore`, `Completeness`).

### 5) Logging and Console UX

- Console app prints lifecycle: creation of subtasks, their completion, aggregation start/finish, final score and sources count.

### 6) Documentation Sync

- Update provider setup in docs to reflect `AIProviderFactory` + `appsettings.json` configuration.
- Clarify this version is LLM-only and web search comes next.

## Web Search Readiness (Not Implemented Yet)

- Define an interface and data model:
  ```csharp
  public interface IWebSearchService { Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int limit, CancellationToken ct); }
  public class WebSearchResult { public string Title { get; set; } public string Url { get; set; } public string Snippet { get; set; } }
  ```
- Future integration points:
  - `ExecutorAgent`: optional enrichment phase before LLM synthesis to fetch top-k results, include citations.
  - `TaskAnalyzerAgent`: optional use to refine subtasks when queries are too broad or ambiguous.
  - Add provider-backed implementation (e.g., Bing, Brave, Tavily) behind the interface.

## Phased Implementation Plan (Small, Reviewable Steps)

### Phase 1: Structured Outputs Refactor (PR1)
- Replace raw `InvokePromptAsync` usages with `WithStructuredOutputRetry<T>` in:
  - `TaskAnalyzerAgent.AnalyzeComplexity`
  - `TaskAnalyzerAgent.DecomposeTask`
  - `ExecutorAgent.IsAtomicTask`
  - `ExecutorAgent.EvaluateQuality`
  - `ExecutorAgent.CheckCompleteness`
- Add DTOs in a small file `Agents/StructuredOutputs.cs` or near agent classes.
- Update prompts to explicitly “return JSON matching schema only”.

### Phase 2: Decomposition Fix and Mapping (PR2)
- Change decomposition schema to `string[]`.
- Map to `ResearchTask` and enqueue subtasks with `ParentTaskId`.
- Add console logs for subtask creation.

### Phase 3: Aggregation Wiring (PR3)
- Inject `GetChildren` into `AggregatorAgent`.
- Implement `GetSubTaskResults()` using the injected delegate.
- Ensure parent is re-enqueued when children are all `Completed` (already present); have aggregator synthesize final.

### Phase 4: Executor Sources and Result Mapping (PR4)
- Update executor prompt to return `content` and `sources`.
- Map to `ResearchResult`, keep quality score and completeness checks structured.
- Console prints sources count and shows a few sources.

### Phase 5: Docs and UX Polish (PR5)
- Update `docs/ResearchAgentNetwork-Implementation.md` sections for provider factory and new structured-output behavior.
- Add short “How to run” and “What to expect” steps.
 - Create per-phase summaries (`SimpleResearchAssistant-Phase2.md`..`Phase4.md`) with testing steps.

## Testing Strategy

- Unit tests: DTO deserialization happy-paths and error cases (no LLM calls).
- Integration tests (optional): existing Ollama tests continue to pass; add one end-to-end console run smoke test if desired.
- Manual E2E: run console app and verify task moves through states, subtasks created, final aggregation produced.

### Commands
```bash
# Build
 dotnet build

# Run console app
 dotnet run --project ResearchAgentNetwork.ConsoleApp

# Run all tests
 dotnet test

# Only LLM tests (will skip if Ollama not available)
 dotnet test --filter "FullyQualifiedName~TaskAnalyzerAgentLLMTests"
```

## Acceptance Criteria

- Simple complex prompt decomposes into 3–5 subtasks (strings mapped to tasks with parent set).
- Atomic tasks execute and return `ResearchResult` with `Content`, `ConfidenceScore`, and `Sources` (claimed).
- When all children complete, parent is aggregated into a cohesive synthesis.
- All structured prompts use `WithStructuredOutputRetry<T>` with stable parsing (no dictionary lookups).
- Console app displays lifecycle updates and final output.
- Documentation reflects factory config and the LLM-only scope.

## Risks and Mitigations

- LLM JSON drift: mitigated by `WithStructuredOutputRetry<T>` and explicit schema prompts.
- Provider variability: keep prompts concise, avoid over-constraining; test with Ollama defaults.
- Concurrency/race conditions: orchestration already throttled; aggregation only enqueues when all children complete.

## Future Enhancements (Next Iteration)

- Web search integration behind `IWebSearchService` with pluggable provider.
- Embedding-based similarity and merging (complete `TaskMergerAgent`).
- Persistence (EF Core) for tasks/results and resume-on-restart.
- Result caching and deduplication.
- Web API + UI for submission and monitoring.
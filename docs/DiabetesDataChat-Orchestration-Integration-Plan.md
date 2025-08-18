# DiabetesDataChat Orchestration Integration Plan

## Goal

Enable DiabetesDataChat to split prompts and process them through a multi‑agent, task‑based orchestration similar to `ResearchAgentNetwork`, using a queue, decomposition, execution, aggregation, and quality assessment loop. Keep existing Diabetes agents (SQL, Writer, Quality Controller) and wire them in via a thin adapter layer. Prefer Semantic Kernel native capabilities where it helps, but keep the orchestration deterministic and testable.

## Scope

- Integrate a minimal set of orchestration and domain components.
- Reuse Diabetes agents for domain work; avoid duplicating business logic.
- Add a feature‑flagged entry point so we can roll out gradually.

## What to copy from ResearchAgentNetwork

- **Domain models** (copy as a starting point):

  - `ResearchAgentNetwork.Core/Domain/ResearchTask.cs`
  - `ResearchAgentNetwork.Core/Domain/TaskStatus.cs`
  - `ResearchAgentNetwork.Core/Domain/TaskEvent.cs`
  - `ResearchAgentNetwork.Core/Domain/ResearchResult.cs`
  - `ResearchAgentNetwork.Core/Domain/RetrievedItem.cs` (optional now, useful later if adding memory/web search)
  - `ResearchAgentNetwork.Core/Domain/ComplexityAnalysis.cs` (optional)
  - `ResearchAgentNetwork.Core/Domain/QualityAssessment.cs`
  - Optional but present in the current orchestrator pipeline (only if you intend to enable report drafting later):
    - `ResearchAgentNetwork.Core/Domain/ReportOutline.cs`
    - `ResearchAgentNetwork.Core/Domain/ReportSectionDraft.cs`

- **Agent abstraction and core agents**:

  - `ResearchAgentNetwork.Core/Agents/IResearchAgent.cs`
  - `ResearchAgentNetwork.Core/Agents/TaskAnalyzerAgent.cs` (generic decomposition)
  - `ResearchAgentNetwork.Core/Agents/TaskMergerAgent.cs` (de‑dup/consolidation)
  - `ResearchAgentNetwork.Core/Agents/RetrievalDecisionAgent.cs` (keep; can return "no retrieval" initially)
  - `ResearchAgentNetwork.Core/Agents/QueryPlannerAgent.cs` (keep minimal query‑planning shape)
  - `ResearchAgentNetwork.Core/Agents/ExecutorAgent.cs` (replace internals via adapter to Diabetes agents)
  - `ResearchAgentNetwork.Core/Agents/AggregatorAgent.cs`
  - `ResearchAgentNetwork.Core/Agents/QualityAssessmentAgent.cs` (wire to Diabetes QC agent)
  - Skip `WebSearchAgent.cs` for now.

  - New in the current orchestrator (used in a post‑aggregation drafting pipeline). These can be copied but kept disabled behind a feature flag for Diabetes initially:
    - `ResearchAgentNetwork.Core/Agents/ReportOutlineAgent.cs`
    - `ResearchAgentNetwork.Core/Agents/SectionWriterAgent.cs`
    - `ResearchAgentNetwork.Core/Agents/FactCheckAgent.cs`
    - `ResearchAgentNetwork.Core/Agents/CitationManagerAgent.cs`
    - `ResearchAgentNetwork.Core/Agents/KnowledgeCuratorAgent.cs`
    - `ResearchAgentNetwork.Core/Agents/MemoryRouterAgent.cs`

- **Orchestrator**:

  - `ResearchAgentNetwork.Core/Orchestration/ResearchOrchestrator.cs`

- **Common utilities required by agents/orchestrator**:

  - `ResearchAgentNetwork.Core/Common/KernelExtensions.cs` (Structured JSON I/O + retries)
  - `ResearchAgentNetwork.Core/Common/JsonStuff.cs` (JSON schema generation used by `KernelExtensions`)

## What NOT to copy

- Vector store adapters and memory providers for now.
- Web search service/agent for now.
- UI assets and Svelte UI (not relevant for Diabetes solution).

Note: The orchestrator uses memory‑related hooks opportunistically (e.g., retrieval decision, result storage, curation). If `ISemanticMemoryService` is null, those paths no‑op. It’s safe to keep the agents/classes in the codebase even if memory is disabled.

## Where to place things in DiabetesDataChat

- New folder: `DiabetesDataChat.ApiService/Agents/OrchestrationV2/`
  - `Domain/` (copied domain models; rename namespaces to `DiabetesDataChat.ChatAPI.OrchestrationV2.Domain`)
  - `Agents/` (copied agents; rename namespaces to `DiabetesDataChat.ChatAPI.OrchestrationV2.Agents`)
  - `ExecutorAdapters/` (new; adapters that bridge `IResearchAgent` to Diabetes agents)
  - `ResearchOrchestrator.cs` (copied; namespace `DiabetesDataChat.ChatAPI.OrchestrationV2`)

Keep existing `Agents/Orchestration/` (GroupChat, Hierarchical, etc.) intact; V2 lives side‑by‑side behind a feature flag.

## Adapter design (bridge to existing Diabetes agents)

- **SqlPathExecutorAdapter** implements `IResearchAgent`:
  - For tasks whose intent includes data fetching, call `ManagerAgent.CoordinateSqlWorkflowAsync(...)` or the `SqlExpertAgent` methods directly.
  - Return `ResearchResult` with serialized data and a concise textual summary.
  - Also attach structured context into `task.Metadata["RetrievedContext"]` as a `List<RetrievedItem>` (preferred) or a `List<string>` to align with the current executor context builder.
    - Example when using `RetrievedItem`: set `Kind="db"`, `Snippet` to a compact JSON/table sample, other fields can be null. The executor will treat non‑`web` kinds as memory context.

- **WriterExecutorAdapter** implements `IResearchAgent`:
  - Wrap `WriterAgent.ProcessAsync(...)` using a prompt built from `ResearchTask.Description` plus any `RetrievedContext` or SQL results in `task.Metadata`.

- **QualityAssessmentAdapter** implements `IResearchAgent`:
  - Wrap `QualityControllerAgent.ProcessAsync(...)`, parse JSON into `QualityAssessment` and set `NeedsMoreResearch` based on score/threshold.

- Optionally, a thin **RouterExecutorAgent** (if you prefer a single executor) that inspects task metadata and routes to SQL or Writer adapter.

## Minimal Diabetes‑specific Task decomposition

Use the existing generic `TaskAnalyzerAgent` initially, but bias it (via system prompt tweak) to yield these child tasks when relevant:

1. Identify candidate tables (if data likely needed)
2. Generate SQL query
3. Execute SQL query
4. Draft answer
5. Evaluate answer (QA)

Map these to adapters via the executor and/or by tagging subtasks with an `Intent` in `ResearchTask.Metadata` that the executor reads.

Current executor in ResearchAgentNetwork does not route by `Intent` out‑of‑the‑box. When copying it into Diabetes (`OrchestrationV2`), replace its inner logic to dispatch to the adapters based on `task.Metadata["Intent"]` (fallback to Writer). Keep a `ForceExecute` escape hatch consistent with the upstream orchestrator.

## Execution flow

1. API receives a user question. If feature flag `UseOrchestrationV2` is on, submit it to `ResearchOrchestrator.SubmitResearchTask(...)`.

2. Orchestrator:
   - De‑duplicates, enqueues, computes depth; calls analyzer → possibly forks into subtasks.
     - If semantic memory is available, duplicate detection/merging uses similarity thresholds: `pendingMergeThreshold` (default ~0.9) and `completedReuseThreshold` (default ~0.95) before adding a new task.
   - Before execution, optionally plans retrieval (vector memory). This path only runs if memory is enabled; otherwise it is skipped.
   - Executes via executor adapters (SQL path or Writer path).
   - Aggregates children into a parent result if decomposed.
   - Runs a single QA pass; may spawn follow‑ups if needed (one refinement loop max, as in the research orchestrator).
   - Optional post‑aggregation drafting pipeline (disabled by default for Diabetes): outline → section drafting → fact check → citation normalization; optionally memory routing and curation after storage. Gate these behind a `EnableReportDrafting` feature flag in the Diabetes copy of the orchestrator.
3. Controller polls task status or subscribes to `TaskEventPublished` for streaming progress.

## Data flow

- Input: `userQuestion` → `ResearchTask(Description=userQuestion)`
- Analyzer: `ResearchTask.Metadata["Decomposition"] = List<string>` (optional)
- SQL path: candidate tables → SQL query → DB API → JSON → attach to `task.Metadata["SqlData"]` and mirror a compact form into `task.Metadata["RetrievedContext"]` so the executor context builder can leverage it.
- Writer: builds answer using `SqlData` and/or context → `ResearchResult.Content`
- QA: emits `QualityAssessment` in `task.Metadata["QualityAssessment"]`; may trigger one refinement iteration

Additional metadata keys used by the current orchestrator/executor to be aware of during debugging:
- `ForceExecute` (bool): bypass analysis and execute directly when true.
- `ComplexityAnalysis` (object): analyzer output.
- `Decomposition` (string[]): analyzer decomposition list.
- `QualityScore` (number), `Completeness` (object): executor internal assessments.
- `SectionWriteRequest`, `SectionDraft:<id>`: used by optional drafting pipeline.
- `MemoryRouteApplied`: result of optional memory routing.

## SK and LLM considerations

- Keep using the Diabetes solution’s configured `Kernel` and `IChatCompletionService`. No provider change needed.
- Orchestrator and agents accept a `Kernel`, so we stay within Semantic Kernel native patterns [[memory:5990486]].
- Copy `Common/KernelExtensions.cs` and `Common/JsonStuff.cs` as they are required for structured JSON prompts used across agents.
- If/when moving to Ollama for this solution, only Kernel wiring changes; orchestrator code stays the same. Note: another project uses Ollama as the sole LLM service [[memory:5990558]].

## Implementation phases (small PRs)

1. Skeleton and domain (PR1)

   - Copy domain classes and `IResearchAgent` to `OrchestrationV2/Domain` and `Agents` with namespace fixes.
   - Copy `Common/KernelExtensions.cs` and `Common/JsonStuff.cs`.
   - Add `ResearchOrchestrator.cs` without web search/memory wiring. In the Diabetes copy, gate the post‑aggregation drafting pipeline (outline/section/fact‑check/citations/curation) behind `EnableReportDrafting=false` by default.
   - Add feature flag (`UseOrchestrationV2`) and a minimal API endpoint that submits a task and returns the task id.

2. Executor adapters (PR2)

   - Add `ExecutorAdapters/SqlPathExecutorAdapter.cs`, `ExecutorAdapters/WriterExecutorAdapter.cs`, `ExecutorAdapters/QualityAssessmentAdapter.cs`.
   - Replace the copied `ExecutorAgent`’s inner logic to invoke adapters based on `task.Metadata["Intent"]` (fallback to Writer). Ensure SQL adapter also populates `task.Metadata["RetrievedContext"]` so the executor context builder can incorporate data.

3. Analyzer intent tagging (PR3)

   - Slightly tune `TaskAnalyzerAgent` system prompt so decomposed subtasks carry `Intent` hints (e.g., "CandidateTables", "GenerateSQL", "ExecuteSQL", "WriteAnswer").
   - Ensure the executor routes accordingly.

4. Controller integration and progress events (PR4)

   - Expose `TaskEventPublished` to Diabetes’ logging and/or a streaming endpoint.
   - Add endpoints: `GET /orchestrator/tasks/{id}`, `GET /orchestrator/tasks/{id}/report` using `GenerateTaskReport`.

5. QA loop enablement (PR5)

   - Wire `QualityAssessmentAdapter` and allow a single refinement iteration when QA requests more research.

6. Optional retrieval planning (PR6)

   - Keep `RetrievalDecisionAgent` and `QueryPlannerAgent`; configure them to no‑op unless a vector store is later introduced.

Each PR should target ≤10 files and ≤300 LOC, and be independently testable [[memory:5990543]].

## Testing strategy

- Unit tests:

  - Analyzer: given a question requiring data, ensure it emits subtasks with the expected intents.
  - Executor adapters: mock `DatabaseApiClient` and ensure SQL path returns `ResearchResult` with content and attached data; Writer path returns well‑formed content.
  - QA adapter: JSON parsing into `QualityAssessment` and refinement trigger logic.

- Orchestrator integration tests:

  - End‑to‑end for a typical "age at onset" query with stubbed DB, verifying `TaskEvent` sequence: submitted → analyzing → executing → completed.
  - Report generation via `GenerateTaskReport` returns structured text with decomposition and QA sections when present.

## How to test manually

1. Startup: enable `UseOrchestrationV2` in `appsettings.chatapi.json`.

2. POST a question to the new V2 endpoint; capture the task id.

3. Poll `/orchestrator/tasks/{id}` until `Completed` and read `/orchestrator/tasks/{id}/report`.

4. Verify the answer content includes data when the question requires it; otherwise falls back to Writer.

## Risks and mitigations

- Prompt drift in analyzer causing odd decompositions → keep intents constrained and validate in tests.
- Adapter impedance mismatches → keep adapters small; if needed, push light normalization either into adapters or `ExecutorAgent`.
- Duplicate orchestration path with existing GroupChat/Hierarchical managers → feature flag drives usage; keep both until V2 is stable.

## Refactoring notes (future)

- Abstract executor routing into a single `RouterExecutorAgent` for simplicity.
- Introduce a shared `IAgentTelemetry` to capture token usage and attach to `TaskEvent`.
- If vector memory is desired later, wire `ISemanticMemoryService` into the orchestrator the same way as in `ResearchAgentNetwork` and reuse `RetrievedItem`.

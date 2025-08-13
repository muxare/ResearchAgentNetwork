## Research Agent Network — RAG Implementation Plan

This document analyzes the provided agentic RAG pseudo-code and maps it to the current `ResearchAgentNetwork` architecture, proposing a concrete, phased plan to implement robust Retrieval-Augmented Generation (RAG). In this context, the `user_prompt` is equivalent to a submitted research `task`.

### Pseudo-code (reference)

```
function agenticRAG(user_prompt):
    context = initialize_context(user_prompt)
    if agent_decision_requires_retrieval(context):
        query = generate_search_query(context)
        embeddings = embed(query)
        documents = vector_db.retrieve(embeddings, top_k=5)
        context.knowledge += documents
    llm_input = format_prompt(context)
    llm_response = LLM.generate(llm_input)
    final_answer = agent_review_and_refine(llm_response)
    if agent_decision_store(final_answer):
        new_embedding = embed(final_answer)
        vector_db.store(new_embedding, final_answer)
    return final_answer
```

---

## Current Capabilities (as-is)

- Orchestrator (`ResearchOrchestrator`)
  - Before execution, retrieves prior results from vector memory via `ISemanticMemoryService.RetrieveSimilarResultsAsync` and places snippets in `task.Metadata["RetrievedContext"]`.
  - Indexes task descriptions on submit and result content on completion (`IndexTaskAsync`, `IndexResultAsync`).
  - Emits `TaskEventPublished` for SSE and persistence.

- Memory (`ISemanticMemoryService`, `InMemoryVectorStore` or Qdrant via `SkVectorStoreAdapter`)
  - Indexes tasks/results and retrieves similar results. Configurable `TopK`.

- Web search
  - Feature-flagged enrichment step using `IWebSearchService`. We now support SK-native `TavilyTextSearch` behind `SKTavilyWebSearchService` and continue to support an HTTP adapter.

- Executor prompt
  - `ExecutorAgent` builds prompt context using `task.Metadata["RetrievedContext"]` and generates structured output.

These cover parts of Steps 3–5 and Step 7 in the pseudo-code.

---

## Gaps vs. Pseudo-code

1. Retrieval decision (Step 2)
   - No explicit, learned/heuristic decision. Retrieval runs opportunistically; web search behind a flag.

2. Query generation (Step 2)
   - No dedicated query expansion/refinement; current retrieval uses the task description as-is.

3. Document handling (Step 3–4)
   - Retrieved snippets are plain text strings without structured provenance (source URL, scores).
   - No chunk-level metadata (chunk index, total chunks) exposed to prompts.

4. Post-process/critique (Step 6)
   - `QualityAssessmentAgent` exists but not integrated to refine answers with retrieved evidence.

5. Storage policy (Step 7)
   - Always indexes results; no explicit “store decision” gate or summarization pathway.

---

## Design Principles for RAG in RAN

- Retrieval is cheap; use it when uncertainty is high, novelty is likely, or evidence is required.
- Keep agents small and composable; use structured outputs for decisions and queries.
- Propagate provenance (urls, titles, scores, chunk ids) to prompts and final reports.
- Prefer result summaries and chunked storage to control token costs.

---

## Proposed Architecture Mapping

Step 1: initialize_context(user_prompt)
- Map to `ResearchTask` creation. Context is the task plus any known parent/children and recent memory (`RetrievedContext`).

Step 2: agent_decision_requires_retrieval(context)
- Add `RetrievalDecisionAgent : IResearchAgent` or extend `ExecutorAgent` with a structured decision:
  - Inputs: task description, complexity, freshness hints.
  - Output DTO: `{ requireRetrieval: bool, reason: string, retrievalTypes: [ "vector", "web" ] }`.
  - Heuristics: new/news-like terms, low confidence priors, long-tail topics.

Step 2: generate_search_query(context)
- Add `QueryPlannerAgent : IResearchAgent` to propose 1–3 refined queries.
  - Input: task, optional keywords from `TaskAnalyzerAgent`.
  - Output DTO: `{ queries: string[] }`.

Step 3: retrieve from Vector DB
- Use existing `ISemanticMemoryService.RetrieveSimilarResultsAsync` with the best query.
- Extend to return structured payloads: include confidence, chunk indices, and source tags (when stored). Store these in `task.Metadata["RetrievedContext"])` as objects, not strings.

Step 3 (optional): web retrieval
- Use `SKTavilyWebSearchService` to fetch `topK` sources, each with `{ title, url, content }`. Append to context with provenance.

Step 4: add documents to context
- Adjust `ExecutorAgent.BuildContext` to format a compact, provenance-rich context section:
  - `[{ kind: "vector", score, snippet, tags, chunkIndex }, { kind: "web", title, url, snippet }]`.

Step 5: LLM generate
- Reuse `ExecutorAgent.ExecuteWithLLM`. Emphasize: “Use only provided evidence where applicable; include citations.”

Step 6: post-process
- Integrate `QualityAssessmentAgent` to: (a) score answer quality, (b) detect missing coverage, and (c) optionally request more retrieval/queries in a single refinement pass.

Step 7: store decision
- Add `StoreDecisionAgent` (or a lightweight policy method) to decide to store:
  - Criteria: length threshold, confidence ≥ τ, novelty vs. existing memory, topic whitelist.
  - If accepted: index a summary + chunked content with provenance; otherwise skip.

---

## Implementation Plan (small, reviewable PRs)

PR1: Structured retrieval decision and query planning
- Add `RetrievalDecisionAgent` with DTO `{ RequireRetrieval, Reason, RetrievalTypes[] }`.
- Add `QueryPlannerAgent` returning `{ queries: string[] }`.
- Orchestrator: before current vector/web retrieval, call decision → query planner → pick best query.
- Tests: decision heuristics, query planner happy-path.

PR2: Provenance-rich context and DTOs
- Change `RetrievedContext` from `List<string>` to `List<RetrievedItem>`:
  ```csharp
  public record RetrievedItem(string Kind, string Snippet, string? Title, string? Url, double? Score, int? ChunkIndex, int? TotalChunks);
  ```
- Update `ExecutorAgent.BuildContext` to render compact, token-bounded evidence with citations.
- Tests: context formatting, length bounding.

PR3: Memory indexing improvements
- Enhance `SemanticMemoryService.IndexResultAsync` to support chunking metadata (`ChunkIndex`, `TotalChunks`) and optional source tags.
- Optional: store short auto-summary per result; index both summary and content.
- Tests: indexing with chunk metadata; retrieval returning payloads.

PR4: Integrate QA refinement loop
- After `ExecutorAgent` response, run `QualityAssessmentAgent`:
  - If low quality or gaps: allow one refinement cycle with additional retrieval using planned queries.
- Tests: refinement triggers and stop conditions.

PR5: Store decision policy
- Add a simple policy (function or tiny agent) to decide storage.
- If accepted: index summary + chunked content; attach provenance.
- Tests: store/skip logic.

PR6: Config and observability
- New settings under `ResearchAgent:Rag` (e.g., `TopK`, thresholds, allowlists).
- Emit `ingested`, `retrieved`, `refined` events with counts and sources.
- UI: surface retrieval counts and citation presence.

---

## Data Flow (updated)

1) Submit Task → Orchestrator enqueues.
2) Decision → Query planning → Retrieval (vector + optional web) → `RetrievedContext` (rich items).
3) Executor builds prompt with evidence and generates output with citations.
4) QA reviews → optional refinement loop.
5) Store decision → index (summary + chunks) with provenance.

---

## Testing Strategy

- Unit tests:
  - Decision and planner DTOs and heuristics.
  - Context formatting and token caps.
  - Store policy decisions.
- Integration tests:
  - Retrieval end-to-end with in-memory vector store.
  - Web retrieval behind feature flag (mocked provider for deterministic output).
- LLM tests (optional):
  - Evidence-grounded generation with citations.

---

## Risks & Mitigations

- Token bloat: enforce strict caps and summaries.
- Noisy sources: add domain allowlist and score thresholds.
- Cost: one-pass refinement, low `TopK`, chunking + summaries.

---

## Mapping to Code

- New/updated types
  - `RetrievalDecisionAgent`, `QueryPlannerAgent`, `StoreDecisionAgent` (tiny agents with structured outputs)
  - `RetrievedItem` DTO for `RetrievedContext`

- Touch points
  - `ResearchOrchestrator`: decision + planning + retrieval orchestration; eventing.
  - `ExecutorAgent`: context builder, prompt updates for citations.
  - `SemanticMemoryService`: enriched payloads and chunk metadata.
  - `SKTavilyWebSearchService` (or HTTP adapter): return `{ title, url, content }` consistently.

---

## Configuration (proposed)

```
"ResearchAgent": {
  "Rag": {
    "EnableWeb": true,
    "VectorTopK": 5,
    "WebTopK": 5,
    "StoreMinConfidence": 0.6,
    "AllowDomains": ["arxiv.org", "nasa.gov", "who.int"]
  }
}
```

---

## Summary

This plan elevates RAG from opportunistic retrieval to a principled pipeline with: (1) retrieval decision, (2) query planning, (3) provenance-rich context, (4) QA-driven refinement, and (5) store policy. Changes are incremental, testable, and align with the system’s existing agent/orchestrator design.


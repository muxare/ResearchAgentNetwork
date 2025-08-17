## Vector Database — Phase 2: Advanced Similarity

### What was implemented

- Advanced retrieval with Maximal Marginal Relevance (MMR) re-ranking and a dynamic relevance threshold.
- Applies to both `RetrieveSimilarTasksAsync` and `RetrieveSimilarResultsAsync` in `SemanticMemoryService`.
- Behavior:
  - Fetches an expanded candidate set from the vector store (default: max(10, 5x topK)).
  - Re-embeds candidate payloads and computes cosine similarities to the query.
  - Filters items below 70% of the best relevance score (dynamic threshold).
  - Re-ranks remaining items using MMR (alpha=0.7) to balance relevance vs. diversity.

### Why it matters

- Reduces near-duplicate results and improves topical coverage of the final top-K.
- Improves task de-duplication since orchestrator’s checks use `RetrieveSimilarTasksAsync`.
- Provides more stable quality independent of the backing vector store’s default scorer.

### Code changes (high-level)

- File `ResearchAgentNetwork.Infrastructure/SemanticMemory/SemanticMemoryService.cs`:
  - Both retrieval methods now:
    - Expand initial fetch window
    - Re-embed candidate payloads (batch)
    - Apply dynamic threshold and MMR rerank
  - Added minimal cosine helper and `RerankWithMmrAsync` routine

### Data flow

1. Query text → embed via SK embedding provider
2. Vector store search (Qdrant or in-memory) returns candidate `VectorQueryResult`s with payloads
3. Candidate payloads → batch embeddings → cosine relevance vs. query
4. Threshold filter → MMR re-ranking → final ordered results (size = topK)

### Execution flow touchpoints

- Orchestrator uses `RetrieveSimilarTasksAsync` during submission for merging/dedup heuristics. The improved ranking feeds better candidates into those checks without any orchestrator code changes.
- Agents that call `RetrieveSimilarResultsAsync` (e.g., curation) will see higher-quality, less-redundant contexts.

### How to test

Backend-only validation:
- Set `VectorDb:Provider` to `None` to use the in-memory store or `Qdrant` to use the adapter.
- In Web host, create several tasks with overlapping descriptions. Submit a new, similar task and observe:
  - If a pending task is sufficiently similar, the orchestrator merges into the existing task (no duplicate added).
  - If a completed task is highly similar, the orchestrator returns the existing task id.

End-to-end with Qdrant:
1. Start Qdrant: `docker compose up -d qdrant`
2. Configure:
   - `VectorDb:Provider=Qdrant`
   - `VectorDb:Endpoint=localhost:6334`
3. Run Web: `dotnet run --project ResearchAgentNetwork.Web`
4. Submit multiple related tasks and confirm dedup behavior and higher-quality similar result retrieval.

### Notes and next steps

- Current MMR alpha is set to 0.7 and threshold at 70% of the max relevance. These can be externalized later if needed.
- Next incremental task in the same section: enhance explicit task deduplication/merging logic (threshold tuning, tie-breaking, and optional human-in-the-loop) in the orchestrator and `TaskMergerAgent`.


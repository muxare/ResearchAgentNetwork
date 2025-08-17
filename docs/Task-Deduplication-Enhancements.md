## Task Deduplication Enhancements

### What was implemented

- Orchestrator deduplication now:
  - Uses configured retrieval `topK` for candidate selection instead of a hardcoded value.
  - Selects the most similar candidate (pending/completed) rather than the first result.
- `TaskMergerAgent` accepts `ISemanticMemoryService` (for future richer merges). Currently, it defers to orchestrator for actual merge side-effects.

### Why it matters

- More reliable duplicate detection with better candidate ordering.
- Predictable behavior that prioritizes the strongest semantic match.

### How to test

1. Ensure vector memory is enabled (optional but recommended for best results):
   - `VectorDb:Provider=Qdrant` and `VectorDb:Endpoint=localhost:6334` (or `None` for in-memory).
2. Run the Web host and submit:
   - Task A: "Summarize the findings on diabetes risk factors"
   - Task B: "Create a summary of known diabetes risk contributors"
   - Task C: "Summarize diabetes risk factors in adults"
3. Submit a new similar task (Task D). Observe:
   - If a pending similar task exists above pending threshold, it merges into the most similar one.
   - If a highly similar completed task exists above completed threshold, its id is returned.

### Configuration notes

- Thresholds are controlled via appsettings/environment:
  - `ResearchAgent:Merging:PendingThreshold`
  - `ResearchAgent:Merging:CompletedThreshold`
  - Retrieval size: `VectorDb:TopK`


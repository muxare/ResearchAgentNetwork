<!-- markdownlint-disable MD041 -->
## Phase B: Memory and Text Processing

### Overview
This phase introduces the synthesis entry point by generating a minimal report outline from a completed parent task, while continuing to use Semantic Kernel for text processing. It keeps changes small and testable.

### What Changed
- Added domain types: `ReportOutline`, `OutlineSection`
- Added `ReportOutlineAgent` that produces a structured outline via SK structured outputs
- Wired the outline stage into `ResearchOrchestrator` immediately after parent aggregation
- Emits a `outlined` event with section count

### Data Flow
1) Child subtasks complete (or permanently fail)
2) Parent enters Aggregating → `AggregatorAgent` composes parent `ResearchResult`
3) `ReportOutlineAgent` generates outline from the parent task description and context
4) Outline stored in `task.Metadata["ReportOutline"]` for future phases

### Execution Flow
- No changes to submission or execution steps; outline stage is called automatically post-aggregation

### Configuration
No new settings required for Phase B.

### How to Test
1) Build and run as in Phase A
2) Submit a task that decomposes into subtasks
3) Stream events (`/api/events`) and look for `aggregated` followed by `outlined`
4) Retrieve the parent task via `/api/tasks/{id}` and inspect `metadata.ReportOutline`

### Notes
- The outline schema is minimal by design and will be expanded in later phases (acceptance criteria refinement, evidence IDs binding)
- Text processing uses the existing SK-based structured output extensions

### Next (Phase C preview)
- Section drafting constrained by evidence IDs
- Integrate QA and retrieval into the section drafting loop


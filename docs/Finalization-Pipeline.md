# Finalization Pipeline (Outline → Sections → Fact Check → Citation Normalize)

## Overview

When a root task completes (either via aggregation or direct execution), the orchestrator enqueues a set of system subtasks in the `Finalization` category:

1. Outline: `ReportOutlineAgent`
2. Section Drafting: `SectionWriterAgent`
3. Fact Checking: `QualityAssessmentAgent`
4. Citation Normalization: `CitationManagerAgent`

These appear as task cards in the UI and emit SSE events for progress.

## Data Flow

- Root completion → orchestrator `EnqueueFinalizationPipeline(root)`
- Subtasks chained with `DependsOn` metadata; a simple gating re-enqueues blocked tasks until dependency completes
- Outputs are persisted to `TaskReportEntity` by existing hooks in `Program.cs` when `completed`/`failed` events fire

## Execution Flow

1. Orchestrator publishes `submitted` for each finalization subtask
2. Each subtask runs its agent; upon completion the next dependent step becomes unblocked
3. SSE events: `submitted`, `blocked`, `agent_started`, `completed`, plus step-specific messages

## How to Test

1. Submit a root task via `POST /api/tasks` and wait for completion
2. Observe new subtasks (category `Finalization`) via `/api/tasks` and SSE `/api/events`
3. Verify ordering: Outline → SectionDraft → FactCheck → CitationNormalize
4. Confirm persisted report endpoints return content:
   - `GET /api/tasks/{id}/report` (runtime)
   - `POST /api/tasks/{id}/report` (persist)
   - `GET /api/tasks/{id}/report/persisted`

## Notes

- This PR adds scaffolding and eventing; PR 3 wires full agent execution and retries.

## UI Notes (PR 2)

- Finalization subtasks are visible as normal cards across the Kanban columns with two badges:
  - `system` badge for system-generated tasks
  - `finalization` badge for tasks with category `Finalization`
- Live progress appears as cards move columns based on SSE-driven status updates
- Dependency gating emits a temporary `blocked` event; the card remains in its column with normal `Pending`/`Analyzing` states until unblocked


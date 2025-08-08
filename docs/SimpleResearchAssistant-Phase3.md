# Phase 3 – Aggregation Wiring

## Summary
- Enable parent aggregation when all children complete by injecting a `GetChildren` delegate into the aggregator and handling `Aggregating` status in the orchestrator.

## Changes
- `AggregatorAgent` now takes a `Func<Guid, List<ResearchTask>>` to fetch children from the orchestrator’s registry.
- `ResearchOrchestrator.InitializeAgents()` wires the delegate to query `_taskRegistry`.
- `ResearchOrchestrator.ProcessSingleTask()` now handles `TaskStatus.Aggregating` by invoking the aggregator and completing the parent with an aggregated `ResearchResult`.
- Added logs for aggregation completion.

## Architecture/Execution Flow Impact
- When a child finishes, `CheckParentAggregation` sets the parent to `Aggregating` and re-enqueues it if all children are complete.
- The aggregator can now collect children, synthesize results, and finalize the parent task.

## How to Test

### Build and run tests
```bash
cd ResearchAgentNetwork
dotnet build
dotnet test
```

### Manual run
```bash
dotnet run --project ResearchAgentNetwork.ConsoleApp
```
- Submit a complex task; expect:
  - Initial decomposition into 3–5 subtasks
  - Subtasks complete
  - Parent transitions to `Aggregating`
  - Final aggregated result printed

## Expected Results
- Parent tasks with completed children produce a synthesized `ResearchResult` including combined sources and average confidence.

## Notes
- This stays LLM-only. Web search integration can later enrich child results’ sources.
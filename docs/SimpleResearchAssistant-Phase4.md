# Phase 4 – Executor Structured Sources and Result Mapping

## Summary
- Make executor return structured `content` and `sources`, then map into `ResearchResult`.
- Keep quality and completeness checks structured.

## Changes
- `ExecutorAgent.ExecuteWithLLM` now uses `WithStructuredOutputRetry<ExecutorStructuredResult>` and maps results to `ResearchResult` including `Sources`.
- Added `ExecutorStructuredResult` DTO with `Content` and `Sources`.

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
- Submit a task; expect the final result to include a non-empty `Sources` list when the model provides citations.

## Notes
- Sources are LLM-claimed in this phase; later web search can validate/augment them.
## Web Search — Query Planning Integration

### What was implemented

- Orchestrator stores planned queries from `QueryPlannerAgent` into `task.Metadata["PlannedQueries"]`.
- `WebSearchAgent` now uses these planned queries (if available) instead of only the raw task description.
- Results from all planned queries are aggregated into a single `RetrievedContext` with provenance.

### Why it matters

- Better coverage and precision for web search by issuing multiple focused queries.
- Enables future ranking/selection over multiple query result sets.

### How to test

1. Run the Web host with web search enabled and a configured provider (e.g., Tavily).
2. Submit a task likely to benefit from multiple facets (e.g., "Recent advances in Type 2 diabetes treatments and side effects").
3. Observe:
   - Orchestrator events show `query_planner` with generated queries.
   - `websearch` agent fetches results across these queries and aggregates them.
   - `RetrievedContext` contains web items with `title` and `url`.


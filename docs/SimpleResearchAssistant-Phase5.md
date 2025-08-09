## Simple Research Assistant — Phase 5: Web Frontend, Live Updates, Runtime Settings, and Execution Robustness

This phase adds a minimal web app for interaction, live progress via SSE, runtime settings, and robustness fixes for execution completion and JSON parsing.

### What’s Included
- New project: `ResearchAgentNetwork.Web`
  - Minimal API endpoints: submit tasks, list tasks, task by id, report, progress, SSE events.
  - Serves static UI (`wwwroot/index.html`, `wwwroot/app.js`).
- Live updates (SSE): server pushes progress snapshots on connect and task events as they happen.
- Runtime settings endpoint: `POST /api/settings` to update `MaxDecompositionDepth` and `LogPrompts` without restarts.
- Orchestrator events: `TaskEventPublished` for submitted/status/decomposed/aggregated/completed/failed.
- Execution robustness:
  - Force execution path at max depth or after a declined “atomicity” check.
  - JSON repair logic to handle unescaped CR/LF/tab characters inside LLM JSON string values.

### Files Changed
- `ResearchAgentNetwork.Web/Program.cs`: endpoints, SSE, settings, DI reusing your AI provider and kernel.
- `ResearchAgentNetwork.Web/wwwroot/*`: simple UI to submit tasks, view tasks, open reports, and adjust settings.
- `ResearchAgentNetwork.ConsoleApp/Program.cs`:
  - `TaskEvent` and `TaskEventPublished` added to `ResearchOrchestrator`.
  - `UpdateMaxDecompositionDepth(int)` added.
  - Publish events on lifecycle changes; set `ForceExecute` at max depth; single forced execution fallback.
- `ResearchAgentNetwork.ConsoleApp/KernelExtensions.cs`:
  - `RepairInvalidStringLiterals` escapes CR/LF/TAB inside JSON string literals after extraction, improving deserialization resilience.

### API
- POST `/api/tasks` → `{ id }`
- GET `/api/tasks` → array of tasks
- GET `/api/tasks/{id}` → task
- GET `/api/tasks/{id}/report` → plain text report
- GET `/api/progress` → `{ TaskStatus: count }`
- GET `/api/events` → SSE stream (`type=progress|task`)
- POST `/api/settings` → `{ maxDecompositionDepth?, logPrompts? }`

### How to Run
1. Build: `dotnet build`
2. Run web: `dotnet run --project ResearchAgentNetwork.Web`
3. Open the URL shown (e.g., `http://localhost:5000`).
4. Submit a task in the UI; observe live progress and open the report.

### How to Test Execution Completion
- Set Max Depth to a small value (0 or 1) via the Settings form.
- Submit a broad task (e.g., “Compare TypeScript and JavaScript”).
- Expected:
  - If analyzer declines decomposition but executor declines atomicity, the system will force execution once and complete.
  - Status should change to Completed or Failed; it should not stall in Executing/Analyzing indefinitely.

### Troubleshooting
- If a report previously failed due to JSON parsing (unescaped newlines in `Content`), the repair logic now escapes CR/LF/TAB inside JSON strings; try again.
- Enable “Log Prompts” in Settings to see exact prompt/response in console output for debugging.

### Follow-ups (Optional)
- Emit more granular events directly from agents for finer UI updates.
- Add pagination/filtering in the tasks table and hierarchical task drilldowns.
- Replace in-memory SSE subscriber list with a concurrent collection or channel.

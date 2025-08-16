<!-- markdownlint-disable MD041 -->
## Phase C: Section Writer (Microsoft tools)

### Overview
Introduces a section drafting stage that writes each outline section with evidence-grounded text using SK structured outputs.

### What Changed
- Domain: `ReportSectionDraft`
- Agent: `SectionWriterAgent` (uses SK)
- Orchestrator: triggers section writing for each `OutlineSection` after the outline stage; emits `section_drafted` events

### Data Flow
1) Outline created and stored on parent task metadata
2) For each section, a `SectionWriteRequest` is prepared
3) `SectionWriterAgent` generates a `ReportSectionDraft` (markdown + basic citations)
4) Draft stored under `task.Metadata["SectionDraft:{sectionId}"]`

### Execution Flow
- Occurs immediately after outline generation, within the parent task aggregation branch

### Configuration
No new settings required for Phase C.

### How to Test
1) Submit a task that decomposes and aggregates
2) Watch `/api/events` for `outlined` and multiple `section_drafted` events
3) Inspect `/api/tasks/{id}` metadata entries for `SectionDraft:<sectionId>`

### Notes & Limitations
- Evidence binding uses placeholder IDs until retrieval-by-id is added to memory
- Citations are inline placeholders `[n]` for now; normalization will be added in Phase D

### Next (Phase D preview)
- Add Fact Check and Citation Manager; normalize citations and verify claims
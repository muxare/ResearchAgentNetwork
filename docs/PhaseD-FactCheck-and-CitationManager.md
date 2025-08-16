<!-- markdownlint-disable MD041 -->
## Phase D: Fact Check + Citation Manager

### Overview
Adds automated fact-checking of drafted sections and citation normalization with a bibliography output, leveraging SK structured outputs.

### What Changed
- Domain: `ClaimEvidenceTriple`, `Citation`
- Agents: `FactCheckAgent`, `CitationManagerAgent`
- Orchestrator: after each `section_drafted`, runs fact check and citation normalization; emits `section_fact_checked` and `section_citations_normalized`

### Data Flow
1) Section drafted → FactCheck agent extracts claims and matches evidence
2) Citation Manager normalizes citations, producing a bibliography block
3) Results stored under `FactCheck:{sectionId}` and `Citations:{sectionId}` in task metadata

### Execution Flow
- Both run within the parent aggregation/outline/section loop

### Configuration
No new settings required for Phase D.

### How to Test
1) Submit a task; wait for drafting to complete
2) Watch `/api/events` for `section_fact_checked` and `section_citations_normalized`
3) Inspect `/api/tasks/{id}` metadata for `FactCheck:<sectionId>` and `Citations:<sectionId>`

### Notes
- Evidence lookup currently relies on model/tooling; later phases can add retrieval-by-id
- Bibliography formatting uses LLM normalization and may need post-processing in future phases


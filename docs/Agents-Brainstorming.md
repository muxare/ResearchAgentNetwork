<!-- markdownlint-disable MD041 -->
## Agents Brainstorming for ResearchAgentNetwork

This document captures a structured brainstorm of additional agents that could extend the ResearchAgentNetwork. It focuses on:

- Purpose and scope of each agent
- Inputs, outputs, and key interfaces
- Data flow and execution flow implications
- Risks and metrics
- Phased implementation suggestions and testability

The aim is to keep agents small, composable, and tool-centric so they can be orchestrated flexibly by `ResearchOrchestrator`.

### Guiding Principles

- Small single-responsibility agents that produce measurable artifacts
- Evidence-grounded outputs (sources, citations, provenance) by default
- Deterministic prompts + constrained tools wherever possible
- Idempotent operations; re-runs shouldn't corrupt state
- Clear hand-off contracts between agents (schemas, events, and memory tags)
- **Prefer Microsoft Semantic Kernel native features over custom implementations**

---

## Microsoft Semantic Kernel Features Analysis

### Available Features (Already Using)
- ✅ **Microsoft.SemanticKernel.Plugins.Web** - Tavily web search integration
- ✅ **Microsoft.SemanticKernel.Connectors.Qdrant** - Native vector store
- ✅ **Microsoft.SemanticKernel.Embeddings** - Native embedding generation
- ✅ **Microsoft.Extensions.VectorData** - Vector store data models

### Features We Could Leverage (Not Currently Using)
- 🔄 **Microsoft.SemanticKernel.Plugins.Core** - Built-in core plugins
- 🔄 **Microsoft.SemanticKernel.Plugins.Memory** - Native memory management
- 🔄 **Microsoft.SemanticKernel.Plugins.Document** - Document processing
- 🔄 **Microsoft.SemanticKernel.Plugins.Text** - Text analysis

### What We're Reinventing (Could Use Microsoft Instead)
- ❌ **Web Search**: Custom `IWebSearchService` → SK's built-in web plugins
- ❌ **Vector Storage**: Custom `IVectorStore` → SK's native vector connectors  
- ❌ **Document Processing**: Custom chunking → SK's document plugins
- ❌ **Memory Management**: Custom semantic memory → SK's memory plugins

### Implementation Strategy
**Phase 0**: Use existing Microsoft features where possible
**Phase 1**: Replace custom implementations with Microsoft equivalents
**Phase 2**: Extend with Microsoft plugins for new functionality

---

### 1) Web Search and Ingestion Agent

- Purpose: Acquire up-to-date external knowledge and ingest it into Semantic Memory with provenance.
- Inputs: Subtask intent, keywords, domain constraints, freshness requirements, budget (time/cost), and relevance thresholds.
- Outputs: Chunked documents embedded into vector store; normalized source metadata; task events (ingestion summary); optional link graph.
- Key Interfaces:
  - **Microsoft.SemanticKernel.Plugins.Web** (preferred over custom `IWebSearchService`)
  - **Microsoft.SemanticKernel.Plugins.Document** (for chunking and processing)
  - **Microsoft.SemanticKernel.Plugins.Memory** (for semantic memory)
  - `ResearchOrchestrator` for triggers and budgeting
- Flow:
  1. Query generation (expand/contract queries; site/domain filters)
  2. Search → fetch → clean → chunk → embed → tag with task/subtask IDs and provenance
  3. Store in vector DB using SK's native connectors, record source metadata and timestamps
  4. Emit task events for observability and downstream retrieval
- Risks: Crawl noise, duplication, stale content, cost creep
- Mitigations: Domain allowlist, dedup via similarity thresholds, time windows, rate limiting
- Metrics: Documents added, unique domains, recall@k in retrieval tests, ingestion precision (QA sampling)

**Microsoft Integration Opportunities:**
- Replace custom `IWebSearchService` with `Microsoft.SemanticKernel.Plugins.Web`
- Use `Microsoft.SemanticKernel.Plugins.Document` for chunking and processing
- Leverage `Microsoft.SemanticKernel.Plugins.Memory` for semantic memory operations

Suggested types:

- `WebSearchAgent : IResearchAgent`
- `WebSource` (url, title, authors, publishedAt), `WebChunk` (text, sourceId, embeddingId)

---

### 2) Report Outline Planner (Report Architect)

- Purpose: Convert completed task tree results into a structured report outline tied to evidence.
- Inputs: Root task description, subtask graph, each subtask's results and quality scores, user goals/style guide.
- Outputs: `ReportOutline` with hierarchical `OutlineSection`s, each section linked to supporting evidence IDs in memory.
- Key Interfaces: `ResearchOrchestrator`, **Microsoft.SemanticKernel.Plugins.Memory**, `QualityAssessmentAgent` (for gating low-quality sections)
- Flow:
  1. Analyze task graph → derive logical sections (Background, Method, Findings, Limitations, References)
  2. For each section, attach required evidence set and acceptance criteria (facts to cover, figures to include)
  3. Emit an `OutlinePlanned` event with section IDs
- Risks: Over- or under-coverage of scope; outline drift vs. user intent
- Mitigations: Use explicit coverage checks against root task requirements; critic loop (see Decomposition Critic)
- Metrics: Coverage score, reviewer acceptance rate, number of outline revisions

**Microsoft Integration Opportunities:**
- Use `Microsoft.SemanticKernel.Plugins.Text` for text analysis and section planning
- Leverage `Microsoft.SemanticKernel.Plugins.Memory` for evidence retrieval

Suggested types:

- `ReportOutline` (rootTaskId, sections[])
- `OutlineSection` (id, title, purpose, evidenceIds[], acceptanceCriteria[])
- `ReportOutlineAgent : IResearchAgent`

---

### 3) Section Writer Agent (Evidence-Grounded Writer)

- Purpose: Write each report section methodically using only approved evidence.
- Inputs: `OutlineSection`, evidence IDs, style guide, citation style.
- Outputs: Markdown section text with inline citations; section-level metadata (facts used, confidence, unresolved gaps).
- Key Interfaces: **Microsoft.SemanticKernel.Plugins.Memory** (retrieval by evidence IDs), `QualityAssessmentAgent` (post-write quality gate)
- Flow:
  1. Retrieve evidence chunks → plan subsection bullets → write → insert citations
  2. Send to QA → if rejected, refine using failure reasons
  3. Emit `SectionDrafted` event
- Risks: Hallucinations, weak citations, style inconsistency
- Mitigations: Strict retrieval by evidence IDs, claim-to-source checks (see Fact Checker), style templates
- Metrics: QA pass rate, number of revisions, factual error rate

**Microsoft Integration Opportunities:**
- Use `Microsoft.SemanticKernel.Plugins.Text` for text generation and style consistency
- Leverage `Microsoft.SemanticKernel.Plugins.Memory` for evidence retrieval

Suggested types:

- `ReportSectionDraft` (sectionId, contentMd, citations[], factsUsed[], confidence)
- `SectionWriterAgent : IResearchAgent`

---

### 4) Citation and Source Manager Agent

- Purpose: Maintain consistent citations and references; ensure all claims map to verified sources.
- Inputs: Section drafts, source metadata from memory, citation style config.
- Outputs: Normalized citations, bibliography/reference list, broken-link report.
- Key Interfaces: **Microsoft.SemanticKernel.Plugins.Memory**, optional external DOI/CSL resolvers
- Flow: Resolve DOIs → deduplicate sources → generate CSL/APA/IEEE formats → verify that quoted text matches source
- Risks: Resolver outages, inconsistent metadata
- Metrics: Duplicate rate, broken link count, citation coverage (% claims cited)

**Microsoft Integration Opportunities:**
- Use `Microsoft.SemanticKernel.Plugins.Text` for citation parsing and normalization
- Leverage `Microsoft.SemanticKernel.Plugins.Memory` for source metadata retrieval

Suggested types:

- `Citation` (id, sourceId, locator, quoteHash)
- `CitationManagerAgent : IResearchAgent`

---

### 5) Fact Checker Agent (Claim–Evidence Verifier)

- Purpose: Validate section claims with independent retrieval and source cross-checking.
- Inputs: Extracted claims from drafts; candidate sources via search/retrieval.
- Outputs: `ClaimEvidenceTriple[]`, verification score, flags for weakly supported claims.
- Interfaces: **Microsoft.SemanticKernel.Plugins.Web**, **Microsoft.SemanticKernel.Plugins.Memory**
- Flow: Turn each claim into a search; match quotes and numbers; compute support/contradiction scores
- Metrics: Verified claims %, contradiction count; turnaround time

**Microsoft Integration Opportunities:**
- Use `Microsoft.SemanticKernel.Plugins.Web` for independent verification searches
- Leverage `Microsoft.SemanticKernel.Plugins.Text` for claim extraction and analysis

Suggested types:

- `ClaimEvidenceTriple` (claim, evidenceText, sourceId, supportScore)
- `FactCheckAgent : IResearchAgent`

---

### 6) Knowledge Curator / Deduplicator Agent

- Purpose: Reduce memory redundancy and improve retrieval quality via clustering and tagging.
- Inputs: Newly ingested chunks; similarity thresholds; tagging rules.
- Outputs: Merge suggestions, canonical records, normalized tags.
- Interfaces: **Microsoft.SemanticKernel.Plugins.Memory** (native vector store adapters)
- Metrics: Duplication reduction %, retrieval precision/recall delta

**Microsoft Integration Opportunities:**
- Use `Microsoft.SemanticKernel.Plugins.Memory` for native memory operations
- Leverage `Microsoft.SemanticKernel.Plugins.Text` for similarity analysis

Suggested types:

- `MemoryMergeSuggestion` (canonicalId, duplicateIds[])
- `KnowledgeCuratorAgent : IResearchAgent`

---

### 7) Task Result Summarizer Agent

- Purpose: Produce concise, loss-aware summaries of subtask results for efficient downstream use.
- Inputs: Subtask results, artifacts, QA scores
- Outputs: Structured summaries with provenance links
- Interfaces: `AggregatorAgent`, `QualityAssessmentAgent`
- Metrics: Token savings, coverage retention per summary

**Microsoft Integration Opportunities:**
- Use `Microsoft.SemanticKernel.Plugins.Text` for summarization
- Leverage `Microsoft.SemanticKernel.Plugins.Memory` for result storage

Suggested types:

- `TaskSummary` (taskId, bullets[], keyEvidenceIds[])
- `SummarizerAgent : IResearchAgent`

---

### 8) Decomposition Critic Agent

- Purpose: Critique and refine the output of `TaskAnalyzerAgent` prior to execution.
- Inputs: Root task, initial subtask plan
- Outputs: Revised plan with risks, missing steps, sequencing improvements
- Interfaces: `TaskAnalyzerAgent`, `ResearchOrchestrator`
- Metrics: Replan frequency, downstream failure rate reduction

**Microsoft Integration Opportunities:**
- Use `Microsoft.SemanticKernel.Plugins.Text` for plan analysis and critique

Suggested types:

- `PlanCritique` (issues[], proposedChanges[])
- `DecompositionCriticAgent : IResearchAgent`

---

### 9) Memory Router and Tagging Agent

- Purpose: Route content to appropriate namespaces/collections and apply consistent tags.
- Inputs: New chunks, task metadata
- Outputs: Route decisions, tags applied, retention policies
- Interfaces: **Microsoft.SemanticKernel.Plugins.Memory**
- Metrics: Retrieval quality delta, namespace purity

**Microsoft Integration Opportunities:**
- Use `Microsoft.SemanticKernel.Plugins.Memory` for native memory routing and tagging

---

### 10) Risk/Ethics Review Agent (optional)

- Purpose: Identify ethical, legal, or safety risks in findings and recommendations.
- Inputs: Draft sections, sources, user context
- Outputs: Risk annotations, disclaimers, mitigation suggestions
- Interfaces: `QualityAssessmentAgent`

**Microsoft Integration Opportunities:**
- Use `Microsoft.SemanticKernel.Plugins.Text` for risk analysis and content review

---

## Data Flow and Execution Flow

High-level pipeline with proposed agents.

1. Planning
   - `TaskAnalyzerAgent` → initial subtask graph
   - `DecompositionCriticAgent` → critique and refine
2. Evidence Gathering per subtask
   - **Microsoft.SemanticKernel.Plugins.Web** → ingest sources → **Microsoft.SemanticKernel.Plugins.Memory**
   - `KnowledgeCuratorAgent` → dedup/cluster/tag
3. Execution and Summarization
   - `ExecutorAgent` runs subtask logic/tools
   - `SummarizerAgent` produces concise results
   - `QualityAssessmentAgent` gates outputs
4. Synthesis
   - `ReportOutlineAgent` derives `ReportOutline`
   - For each `OutlineSection`:
     - `SectionWriterAgent` drafts from evidence
     - `FactCheckAgent` verifies claims
     - `CitationManagerAgent` normalizes citations
     - `QualityAssessmentAgent` final check
5. Aggregation and Export
   - `AggregatorAgent` composes final Markdown (initial), PDF/HTML later
   - Persist report and references; emit completion event

Observability additions:

- Task/section events (planned, drafted, verified, cited)
- Ingestion metrics and provenance logs

---

## Architecture Impact

### New agents implementing `IResearchAgent`:
- `WebSearchAgent`, `ReportOutlineAgent`, `SectionWriterAgent`,
  `CitationManagerAgent`, `FactCheckAgent`, `KnowledgeCuratorAgent`,
  `SummarizerAgent`, `DecompositionCriticAgent`, `MemoryRouterAgent`

### Domain additions:
- `ReportOutline`, `OutlineSection`, `ReportSectionDraft`, `Citation`, `ClaimEvidenceTriple`, `TaskSummary`, `MemoryMergeSuggestion`

### Service extensions:
- **Microsoft.SemanticKernel.Plugins.Memory**: tagging API, citation store, batch retrieval by evidence IDs
- **Microsoft.SemanticKernel.Plugins.Web**: add query-expansion and metadata normalization helpers
- **Microsoft.SemanticKernel.Plugins.Document**: document processing and chunking
- **Microsoft.SemanticKernel.Plugins.Text**: text analysis and generation

### Orchestration:
- Extend `ResearchOrchestrator` with synthesis stage (outline → section drafting loop)
- Budgeting hooks for search/fact-check passes

---

## Testing Approach

- Unit tests
  - Query expansion determinism (WebSearchAgent)
  - Outline coverage checks against root task requirements
  - Citation normalization and de-duplication
- LLM tests (existing pattern under `ResearchAgentNetwork.ConsoleApp.Tests`)
  - Writer adherence to evidence-only constraint
  - Fact-check support/contradiction scoring thresholds
  - QA pass rates under noisy inputs
- Retrieval evaluation
  - Recall@k / MRR for newly ingested corpora (before/after curation)

---

## Phased Implementation (Microsoft-First Approach)

### Phase A: Microsoft Integration + Web Search MVP
- **Replace custom `IWebSearchService` with `Microsoft.SemanticKernel.Plugins.Web`**
- **Replace custom vector storage with `Microsoft.SemanticKernel.Connectors.Qdrant`**
- **Implement `Microsoft.SemanticKernel.Plugins.Document` for chunking**
- Enhanced `WebSearchAgent` with proper chunking and metadata
- Tests: Microsoft plugin integration, ingestion pipeline, retrieval sanity

### Phase B: Memory and Text Processing
- **Implement `Microsoft.SemanticKernel.Plugins.Memory` for semantic memory**
- **Implement `Microsoft.SemanticKernel.Plugins.Text` for text analysis**
- `ReportOutlineAgent` creates outline and acceptance criteria
- Tests: Microsoft memory operations, coverage vs. root task

### Phase C: Section Writer with Microsoft Tools
- `SectionWriterAgent` constrained to evidence IDs; integrate QA
- **Use `Microsoft.SemanticKernel.Plugins.Text` for generation**
- Tests: hallucination checks; citation presence

### Phase D: Fact Check + Citation Manager
- `FactCheckAgent` + `CitationManagerAgent`; integrate into section loop
- **Leverage Microsoft plugins for verification and citation management**
- Tests: claim–evidence linkage, citation normalization

### Phase E: Advanced Microsoft Features
- `KnowledgeCuratorAgent` and optional `MemoryRouterAgent`
- **Full integration with Microsoft Semantic Kernel ecosystem**
- Tests: duplication reduction, retrieval quality improvement

Each phase should be independently testable, reviewed, and merged within PR size limits.

---

## Microsoft Integration Benefits

### What We Gain:
1. **Reduced Maintenance**: Microsoft maintains the core functionality
2. **Better Performance**: Native implementations are optimized
3. **Easier Updates**: Automatic updates with SK version bumps
4. **Standard Patterns**: Follow Microsoft's recommended practices
5. **Better Testing**: Microsoft's plugins are already tested

### What We Keep:
1. **Custom Orchestration**: Our unique multi-agent workflow
2. **Domain Models**: Research-specific data structures
3. **Quality Gates**: Our custom quality assessment logic
4. **Task Management**: Our hierarchical task decomposition

---

## How to Validate (manual)

- Use the ConsoleApp to run a single root task; observe:
  - **Microsoft plugin integration and performance**
  - Ingestion summary events and new memory entries
  - Outline generation JSON
  - Section drafts with citations
  - Fact-check report with support scores

---

## Open Questions

- Preferred citation style(s) and output format requirements?
- Budgeting model for expensive verification loops?
- **Which Microsoft plugins to prioritize in Phase A?**

---

## Decisions (2025-08-11)

- Report export: start with Markdown only; add HTML/PDF later
- Feature flag: synthesis agents proceed without a feature flag
- **Microsoft Integration: Prioritize Microsoft plugins over custom implementations**

---

## Next Steps (Microsoft-First Approach)

1) **Phase A: Microsoft Integration + Web Search MVP**
   - Replace custom web search with `Microsoft.SemanticKernel.Plugins.Web`
   - Replace custom vector storage with `Microsoft.SemanticKernel.Connectors.Qdrant`
   - Implement `Microsoft.SemanticKernel.Plugins.Document` for chunking
2) Add minimal `ReportOutline` domain model and `ReportOutlineAgent`
3) Wire synthesis stage into `ResearchOrchestrator` (no feature flag)
4) **Evaluate additional Microsoft plugins for Phase B**

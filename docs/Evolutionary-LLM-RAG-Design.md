## Evolutionary + LLMs for RAG in ResearchAgentNetwork

### Goal
- **Objective**: Leverage evolutionary/genetic programming (GP) together with LLMs to automatically improve RAG pipeline quality, efficiency, and robustness.
- **Scope**: Optimize prompts, retrieval parameters, tool selection, workflow topology, and summarization/aggregation strategies; enable auto-adaptation per task/domain under resource constraints.

### Where Evolution Helps
- **Prompt/program synthesis**: Evolve prompts, few-shot exemplars, and chain-of-thought styles for `TaskAnalyzerAgent`, `QueryPlannerAgent`, `WebSearchAgent`, and `AggregatorAgent`.
- **Retrieval optimization**: Evolve query rewriting, number of search results, provider weights (`Tavily`, `SKTavilyWebSearchService`, `NoOpWebSearchService`), `top_k`, chunk sizes, and reranker choice.
- **Workflow evolution**: Evolve the agent DAG: ordering/parallelism of `TaskAnalyzerAgent` → `QueryPlannerAgent` → `RetrievalDecisionAgent` → `WebSearchAgent` → `AggregatorAgent` → `QualityAssessmentAgent`.
- **Tool selection & routing**: Evolve per-task tool/agent selection policies and thresholds; integrate bandit-style online routing.
- **Safety & cost**: Evolve configurations that respect latency and token budgets while maintaining factuality.

### Genome Design (Pipeline as a Program)
Represent an end-to-end pipeline as a typed genome. Each gene controls a module or parameter.

- **Gene families**:
  - Retrieval params: `provider_weights`, `top_k`, `max_parallel`, `timeout_ms`, `query_rewrite_strategy`
  - Embeddings/RAG memory: `vector_store`, `similarity_function`, `reranker`, `chunk_size`, `overlap`
  - Prompts: `task_analyzer_prompt`, `planner_prompt`, `search_prompt`, `aggregator_prompt`, `qa_prompt`
  - Workflow: presence/order of agents, fan-out/fan-in strategies, majority/weighted voting in `AggregatorAgent`
  - Safety/cost: `max_tokens`, `temperature`, `judge_model` vs `worker_model` (both via Ollama)
  - Post-processing: citation threshold, deduping, quote-length policy

- **Encoding**: JSON/YAML with strict schema + constraints.

```json
{
  "version": 1,
  "workflow": ["TaskAnalyzer", "QueryPlanner", "RetrievalDecision", "WebSearch", "Aggregator", "QualityAssessment"],
  "retrieval": {
    "providers": {"tavily": 0.7, "sk_tavily": 0.3},
    "top_k": 6,
    "query_rewrite": "semantic_variants",
    "reranker": "none"
  },
  "memory": {"vector_store": "qdrant", "chunk_size": 800, "overlap": 120},
  "prompts": {
    "task_analyzer": "You are a task analyzer...",
    "planner": "You decompose tasks into sub-queries...",
    "search": "Rewrite the query to maximize recall...",
    "aggregator": "Merge evidence with citations...",
    "qa": "Grade factuality, coverage, and cite sources..."
  },
  "llm": {"model": "ollama:llama3:instruct", "temperature": 0.3, "max_tokens": 1536},
  "post": {"min_citation_count": 2}
}
```

### Fitness Functions
Combine offline and online metrics into a single scalar fitness. Maintain multi-objective views for diagnostics.

- **Offline/automatic**:
  - Quality score from `QualityAssessmentAgent` (factuality, coverage, coherence)
  - Cost (tokens) and latency (ms)
  - Citation integrity (percentage of claims backed by sources)
  - Answer completeness vs reference answers when available

- **Online/user**:
  - Explicit ratings, dwell time, follow-up edit distance, acceptance rate

- **Composite**: fitness = w1*quality − w2*cost − w3*latency + w4*citations_integrity. Weights tunable per environment.

### Evolutionary Operators
- **Initialization**: Seed with current baseline pipeline + random valid variants within constraints.
- **Selection**: Tournament or epsilon-greedy; keep an elite set.
- **Mutation**:
  - Numeric: Gaussian or log-uniform perturbations (e.g., `top_k`, `temperature`).
  - Categorical: Random re-sampling with priors (e.g., `reranker` choice).
  - Structural: Insert/remove/reorder agents; alter fan-out.
  - LLM-guided: Ask an Ollama model to propose an improved prompt, plan variant, or query-rewrite strategy under task context and constraints.
- **Crossover**: One/two-point over JSON subtrees; type-safe merge with conflict resolution.
- **Diversity/novelty**: Penalize population collapse; track behavior signatures (e.g., result diversity, tool usage patterns).

### Architecture Integration (C# / Semantic Kernel)
- **New types**:
  - `PipelineGenome` (Domain): Strongly typed representation + JSON schema validation.
  - `PipelineRunner` (Orchestration): Interprets a `PipelineGenome` by invoking existing agents in order with configured prompts/params.
  - `FitnessEvaluator` (Orchestration): Runs datasets through a genome; aggregates metrics using `QualityAssessmentAgent` and timers/counters.
  - `EvolutionEngine` (Service): Selection, mutation (incl. LLM-guided via `OllamaProvider`), crossover, elitism, resource caps.
  - `GenomeRepository` (Persistence): Store genomes, results, lineage, and artifacts.
  - `ConstraintValidator` (Common): Enforces safety/cost limits before execution.

- **Leverage existing components**:
  - Agents in `ResearchAgentNetwork.Core/Agents/*`
  - Web search services in `ResearchAgentNetwork.Infrastructure/WebSearch/*`
  - Vector store adapters in `ResearchAgentNetwork.Infrastructure/SemanticMemory/*`
  - LLM via `OllamaProvider` only

### Execution Flow
1. Select a population of genomes (with elite carryover).
2. For each genome, run `PipelineRunner` on a mini-batch of evaluation tasks.
3. `FitnessEvaluator` computes multi-metric results and scalar fitness.
4. `EvolutionEngine` performs selection, mutation (incl. LLM-guided prompt/plan edits), and crossover to create the next generation.
5. Repeat until budget or convergence; persist best genomes; gate to online A/B when confident.

### Phased Implementation Plan
1. Phase 0: Offline Harness & Metrics
   - Create a small evaluation set (10–30 tasks with reference summaries/links where possible).
   - Add timing, token, and QA scoring hooks; add reproducible seeds.

2. Phase 1: PipelineGenome + Runner
   - Implement schema, validation, and execution bridging to current agents.
   - Support parameters for retrieval, prompts, and LLM settings.

3. Phase 2: Basic Evolution (No LLM mutations)
   - Numeric/categorical mutations; tournament selection; elitism.
   - Fitness = quality − cost − latency. Persist results.

4. Phase 3: LLM-Guided Mutations
   - Use Ollama to propose prompt/plan variants given task context and prior failures.
   - Add structural mutations (agent insertion/reordering) behind a flag.

5. Phase 4: Online Bandit Routing (Optional)
   - A/B best genomes on live traffic with regret-minimizing allocation under safety caps.

### Testing Strategy
- Unit tests: `PipelineGenome` validation; `PipelineRunner` execution determinism; mutation/crossover invariants.
- Integration tests: Fitness evaluation on a tiny dataset with golden baselines; regression tests for performance envelopes.
- Reproducibility: Log seeds, inputs, outputs, prompts; snapshot best genomes.

### UI (Later)
- SvelteKit panel for population status: best fitness, Pareto front (quality vs cost vs latency), convergence plots, genome diff.

### Risks & Mitigations
- Mode collapse / overfitting → hold-out tasks, novelty pressure, periodic resets.
- Hallucination risk → strict citation checks, QA gating, judge/worker dual-model pattern.
- Cost/latency blowups → constraint validator, per-generation budget, early stopping on low-fitness.
- Prompt drift → archive baselines; change-point detection; rollback.

### MVP Experiment (Concrete)
Goal: Improve web retrieval quality/latency on existing research tasks using only retrieval params and prompts.

Steps:
1. Seed dataset: 15 tasks drawn from prior usage or `TaskAnalyzerAgent` tests (curate into `docs/data/evalset-v1.json`).
2. Implement `PipelineGenome` with: retrieval (`top_k`, provider weights), prompts (analyzer/planner/search/aggregator), and LLM settings (temperature, max_tokens).
3. Implement `PipelineRunner` and `FitnessEvaluator` using `QualityAssessmentAgent` and timers.
4. Implement `EvolutionEngine` with numeric/categorical mutations, crossover, tournament selection, and elitism.
5. Run 20–50 generations with population size 12–24 under strict caps; compare best genome vs baseline.

Success criteria:
- +10% quality score at same or lower cost; or same quality at −20% cost/latency.

### How to Test the MVP (once implemented)
- Offline run:
  - Configure evaluation set path and budgets in `appsettings.json`.
  - Execute console app command to run evolution over N generations (to be added in `ResearchAgentNetwork.ConsoleApp`).
  - Inspect persisted results and best genome artifact; run baseline vs best on the same dataset.

### Next Experiments
- Evolve workflow topology (agent presence/order, fan-out, ensemble voting weights).
- Introduce LLM-guided structural mutations for the planner and aggregator prompts.
- Multi-objective search with Pareto ranking; present front in UI.
- Domain-specific genomes (per topic/user/team) with online routing.

### Open Questions
- Do we allow structural mutations in early phases, or gate them until numeric/categorical convergence?
- What are acceptable per-generation budgets on this hardware and with Ollama models?
- Which evaluation labels can we derive automatically vs requiring curation?

### Notes for This Codebase
- Use existing `OllamaProvider` only for all mutation-LLM and pipeline-LLM interactions.
- Favor Semantic Kernel-native orchestration; keep agent contracts unchanged where possible.
- Keep PRs small (≤10 files, ≤300 LoC). Implement phases as separate PRs with clear scope and tests.


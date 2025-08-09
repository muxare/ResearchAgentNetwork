### Vector Database Integration Plan

This document outlines how to add vector search to the Research Agent Network to improve task similarity, contextual retrieval, result reuse, and quality.

## Objectives
- Improve reasoning context by retrieving relevant prior work (RAG)
- Detect duplicates and merge similar tasks
- Cache and reuse previous results to reduce cost and latency
- Enable post-hoc analysis, analytics, and reporting over prior research

## Primary Use Cases
- Similarity for Task Merging: find semantically similar pending tasks to avoid duplication
- Context Building for Execution: retrieve top-k relevant prior results for the current task
- Follow-up Generation Assist: surface gaps and related prior work to inform follow-ups
- Result Reuse/Cache: avoid re-running work when a highly similar query exists with high-confidence result
- Reporting/Discovery: query prior research by topic, tags, time window

## Collections and Schemas
We will maintain multiple logical collections (aka indexes):
- tasks: embeddings of `ResearchTask.Description`
  - id: `Guid`
  - vector: `float[]`
  - metadata: `{ createdAtUtc, priority, parentTaskId?, status, tags[] }`

- results: embeddings of `ResearchResult.Content` and optionally summaries
  - id: `Guid` (task id)
  - vector: `float[]` (for content and/or summary)
  - metadata: `{ createdAtUtc, confidence, sources[], tags[], version }`

- prompts (optional, feature-flagged): embeddings of prompts used for LLM calls for audit and reproducibility
  - id: `Guid`
  - vector: `float[]`
  - metadata: `{ agent, operation, createdAtUtc, model, temperature, tags[] }`

Notes:
- Always store a small payload with the raw text (truncated if necessary) and enough metadata to filter.
- Consider multi-vector per item: description vector + tags vector + summary vector (if DB supports). Start with single vector per item.

## Embedding Strategy
- Use the existing provider wiring to generate embeddings:
  - OpenAI: `text-embedding-3-small` (or configurable)
  - Azure OpenAI: configured deployment name
  - Ollama: `builder.AddOllamaTextEmbeddingGeneration` already present
- Abstraction:
  - `IEmbeddingService` (Core): `Task<float[]> EmbedAsync(string text)` and `Task<List<float[]>> EmbedBatchAsync(IEnumerable<string>)`
  - Infrastructure implementations per provider reuse current kernel builder configuration
- Vector size and normalization: DB dependent; normalize to unit length on client if DB expects it

## Chunking Strategy (for results)
- Chunk long result content before embedding to avoid losing signal:
  - Default: 800–1200 tokens per chunk with 10–15% overlap
  - Store chunk order in metadata: `{ chunkIndex, totalChunks }`
  - Also store a short auto-summary per result (LLM) for separate “summary” collection or as metadata

## Query Patterns
- kNN similarity with filter:
  - Filter by time window (e.g., last 90 days), tags, provider, confidence ≥ threshold
- Diversified results (MMR) for context building
- Hybrid (optional): BM25 keyword + vector re-rank (phase 2)
- Thresholding for cache hits: if similarity ≥ τ and confidence ≥ γ, reuse result

## Integration Architecture
- Core
  - Interfaces: `IVectorStore`, `IEmbeddingService`
  - DTOs: `VectorRecord`, `VectorQuery`, `VectorQueryResult`
  - Services: `ISemanticMemoryService` orchestrating embeds and vector-store IO
- Infrastructure
  - Providers: Qdrant, pgvector, Pinecone (choose one to start, behind `IVectorStore`)
  - No-op (in-memory) implementation for local/dev
- Persistence
  - Keep EF Core for relational state; vector DB stores embeddings + payloads

### Suggested Interfaces (Core)
- `IVectorStore`
  - `UpsertAsync(collection, id, vector, metadata, payload)`
  - `QueryAsync(collection, vector, topK, filter, withPayload)`
  - `DeleteAsync(collection, id)`
- `ISemanticMemoryService`
  - High-level helpers for task/result indexing and retrieval

## Data Flows
### Ingest
1) On task submission: embed description → upsert into `tasks`
2) On execution completion: chunk result content → embed chunks → upsert into `results`
3) Optional: prompts → embed prompt text → upsert into `prompts`

### Retrieval
- TaskMergerAgent: embed current task description → query `tasks` → surface similar tasks to merge
- ExecutorAgent context build: embed task description → query `results` top-K → include summaries/sources in prompt context
- QualityAssessmentAgent: embed result content → query `results` for related prior → detect coverage/gaps

## Orchestrator Integration Points
- After `SubmitResearchTask`: index task
- After `ExecutorAgent` completion: index result
- Before `ExecutorAgent` prompt: retrieve context (top-K) and include in `BuildContext`
- Optional cache check before execution: if high-similarity, reuse or propose reuse

## Configuration
- `VectorDb:Provider`: `Qdrant|PgVector|Pinecone|None`
- `VectorDb:Endpoint`, `ApiKey`, `Database`, `CollectionPrefix`
- `VectorDb:TopK`, `SimilarityThreshold`, `CacheConfidenceThreshold`
- `Embeddings:ModelId`, `Normalize: true|false`, `ChunkSize`, `ChunkOverlap`

## Tooling Options
- Qdrant (self-hosted, free, gRPC/HTTP, filters, payloads) – good default
- PostgreSQL + pgvector (hybrid SQL + vector) – simple ops, good filters
- Pinecone (managed) – easy to start, SaaS cost

Start with Qdrant locally via Docker for dev parity. Keep provider-agnostic via `IVectorStore`.

## Security & Privacy
- Avoid storing secrets/model responses containing PII
- Allow per-record redaction via metadata flags
- Support deletion (GDPR) by id
- Segment collections by environment and user/tenant in metadata

## Performance & Cost
- Batch embedding on background queue to reduce latency on hot path
- Cache embeddings for identical strings (hash key)
- Cap vector DB payload sizes; store full text in EF storage if needed, reference from vector metadata

## Testing Strategy
- Unit tests for `ISemanticMemoryService` with a fake vector store
- Contract tests per `IVectorStore` provider
- E2E tests: similar task detection, RAG improves answer quality, cache reuse path
- Offline eval datasets for retrieval (small curated set)

## Rollout Plan (Phased)
- Phase 0: Skeleton
  - Add Core interfaces and no-op in-memory vector store
  - Wire orchestration hooks (index on submit/complete, retrieve before execute)
  - Feature flag: `VectorDb.Provider=None` by default
  - PR size: ≤ 10 files

- Phase 1: Qdrant provider
  - Implement `QdrantVectorStore`
  - Add config, docker-compose for local Qdrant
  - Index tasks/results; RAG context in ExecutorAgent
  - Tests: contract + smoke

- Phase 2: Quality & Cache
  - Add result cache policy (thresholds τ, γ)
  - MMR diversification for context
  - Hybrid optional keyword+vector re-rank
  - Telemetry for hit rates / latency

- Phase 3: Advanced
  - De-duplication and task merge automation
  - Re-embedding/backfill job when models change
  - Analytics endpoints (top topics, similar clusters)

## Minimal API Additions (Web)
- `GET /api/search?query=...&topK=` → returns aggregated matches across `tasks`/`results`
- `POST /api/settings/vector` → runtime tuning (topK, thresholds, provider off/on)

---

## Relational vs Vector Storage Strategy

- Keep both: a vector store is not a relational DB replacement. Use SQL (EF Core) for authoritative state (tasks, statuses, audit, reporting) and a vector DB for similarity and RAG.
- Vector DBs return top-K nearest items with IDs/payload/metadata and allow simple filters. They don’t offer joins, constraints, multi-row transactions, or rich aggregations like SQL.

### Single-Store vs Dual-Store
- Single store: PostgreSQL + pgvector
  - Pros: one DB, ACID, no cross-system sync; SQL for rows plus vector similarity in one place.
  - Cons: vector features/perf may lag specialized engines at large scale.
- Dual store: SQL + specialized vector DB (Qdrant/Pinecone)
  - Pros: best vector features, scale, and tooling.
  - Cons: requires sync between systems; acceptable as eventual consistency.

### Recommended Dual-Store Sync Pattern
- Source of truth: SQL rows with stable IDs.
- Derive embeddings: background worker computes embeddings and upserts to vector DB with the same ID.
- Event-driven: use an Outbox table or domain events to emit create/update/delete for embedding jobs.
- Deletes: delete vector by ID on row delete.
- Versioning: store `embeddingVersion`; run a backfill job when the model changes.
- Read path: vector search → IDs → hydrate details from SQL to avoid stale payloads.

### When Single Store Is Enough
- If you choose Postgres + pgvector, store vectors in a column on the same rows and index with ivfflat/hnsw. This avoids cross-system sync and is the simplest operationally.

### Practical Guidance for This Project
- Choose pgvector (simplest) or Qdrant+SQL (if you want a dedicated engine now).
- Model in SQL (authoritative): tasks, results, metadata.
- Model in vector DB: embeddings per task/result chunk with metadata `{id, type, createdAtUtc, chunkIndex, totalChunks, confidence, tags[], embeddingVersion}`.
- Retrieval: vector top-K → hydrate from SQL → use for RAG/context/merging.
- Expect eventual consistency; it’s acceptable for retrieval and caching tasks.

## Risks & Mitigations
- Embedding drift: add versioning and re-embed job
- Cost: cache, batch, limit chunking
- Hallucination from RAG: cite sources in prompt, keep top-K small, prefer summaries over raw chunks
- Privacy: support per-record redaction, environment segregation

## Open Questions
- How aggressively to cache/reuse results? human-in-the-loop toggle?
- Single vs multi-collection search for context building?
- Per-agent specialized memories vs global memory?

## Appendix: Example Metadata
- Task record metadata
  - `{ taskId, parentTaskId?, status, priority, createdAtUtc, tags[] }`
- Result record metadata
  - `{ taskId, confidence, createdAtUtc, chunkIndex, totalChunks, sources[], tags[], embeddingVersion }`
  
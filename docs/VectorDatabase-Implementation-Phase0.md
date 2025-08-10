### Vector Database – Phase 0 (Skeleton)

This phase introduces modular semantic memory abstractions and optional in-memory wiring with zero behavior change by default.

Objectives
- Add interfaces to keep code decoupled from any specific vector DB
- Provide a simple in-memory vector store and embedding service
- Add optional hooks in orchestrator for indexing and retrieval
- Leave feature disabled by default via configuration

What was added
- Core (`ResearchAgentNetwork.Core`)
  - `SemanticMemory/Abstractions.cs` with:
    - `IVectorStore`, `IEmbeddingService`, `ISemanticMemoryService`
    - `VectorRecord`, `VectorQueryResult`
- Infrastructure (`ResearchAgentNetwork.Infrastructure`)
  - `SemanticMemory/InMemoryVectorStore.cs` – cosine-similarity, in-memory
  - `SemanticMemory/EmbeddingService.cs` – uses SK `ITextEmbeddingGenerationService`
  - `SemanticMemory/SemanticMemoryService.cs` – high-level helpers
- Orchestrator hooks
  - On submit: index task description (if memory enabled)
  - Before execute: retrieve top-K prior results to enrich context (stored in `ResearchTask.Metadata["RetrievedContext"]`)
  - On completion: index result content
- Entry points
  - Console/Web conditionally construct `ISemanticMemoryService` when `VectorDb:Provider != "None"`

Configuration
- `VectorDb:Provider`: `None` (default) to keep feature off
- Future: `Qdrant|PgVector|Pinecone` will be supported via provider-specific stores

Testing instructions
1) Ensure embeddings are configured (provider already wires embeddings via `AIProviderFactory`)
2) Enable memory (temporary switch):
   - Console/Web `appsettings.json`:
     ```json
     {
       "VectorDb": { "Provider": "InMemory" }
     }
     ```
3) Run console app and submit a few semantically related tasks. Observe:
   - No errors; tasks complete as before
   - `RetrievedContext` present in `ResearchTask.Metadata` for later tasks

Notes
- Phase 0 uses single-vector per record, no chunking, no filters
- Next phase adds Qdrant provider + docker-compose, indexing policies, and retrieval configuration

How to extend (Phase 1)
- Implement `QdrantVectorStore` behind `IVectorStore`
- Add `VectorDb:Endpoint`, `ApiKey`, `TopK`, thresholds
- Switch wiring in Console/Web from `InMemoryVectorStore` to `QdrantVectorStore` when `VectorDb:Provider == "Qdrant"`


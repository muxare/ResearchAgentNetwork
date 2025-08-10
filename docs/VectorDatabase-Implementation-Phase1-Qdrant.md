### Vector Database – Phase 1 (Qdrant Adapter)

Scope
- Introduce `QdrantVectorStoreAdapter : IVectorStore` using Semantic Kernel's Qdrant connector
- Optional wiring in Console/Web via `VectorDb:Provider=Qdrant`
- Local dev via `docker-compose.yml` (ports 6333/6334)

How it works
- We keep our `ISemanticMemoryService` unchanged
- When `Provider=Qdrant`, the adapter is used instead of the in-memory vector store
- The adapter uses SK DI registration for Qdrant client and vector store as recommended

Configuration
```json
{
  "VectorDb": {
    "Provider": "Qdrant",
    "Endpoint": "http://localhost:6333",
    "CollectionPrefix": "ran",
    "TopK": 5
  }
}
```

Local setup
```bash
docker compose up -d qdrant
```

References
- Semantic Kernel overview: https://learn.microsoft.com/en-us/semantic-kernel/overview/
- SK Qdrant connector: https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/qdrant-connector?pivots=programming-language-csharp
- Qdrant docs: https://qdrant.tech/documentation/

Testing
- Set `VectorDb:Provider=Qdrant` in `appsettings.json` (Console/Web)
- Run console app and submit tasks; verify no errors and embeddings are stored
- Inspect Qdrant at `http://localhost:6333` (HTTP API) for created collections named with prefix


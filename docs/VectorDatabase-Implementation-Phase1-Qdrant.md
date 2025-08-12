## Vector Database — Phase 1: Qdrant (Active)

This phase enables Qdrant-backed semantic memory in both Console and Web hosts.

- Provider selection via `VectorDb:Provider=Qdrant` and `VectorDb:Endpoint` (defaults `localhost:6334` gRPC)
- Collections are created using `SkVectorStoreAdapter` with fixed dimension 768
- Hooks:
  - On task submit → index task description
  - Before execute → retrieve similar results for context
  - On completion → index result content

How to run locally

1. Start Qdrant:
```bash
docker compose up -d qdrant
```
2. Set config:
```bash
setx VectorDb__Provider Qdrant
setx VectorDb__Endpoint http://localhost:6334
```
3. Run Web:
```bash
dotnet run --project ResearchAgentNetwork.Web
```

Troubleshooting

- If gRPC 6334 is not reachable, the app will continue without vector memory.
- Change `VectorDb:CollectionPrefix` to isolate runs.

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
    "Endpoint": "localhost:6334",
    "CollectionPrefix": "ran",
    "TopK": 5
  }
}
```

Local setup
```bash
docker compose up -d qdrant
```

Notes
- The application uses the Qdrant gRPC endpoint (port 6334) via `QdrantClient(host, port)`. If you supply an HTTP URL like `http://localhost:6333`, it will be normalized to `localhost:6334` under the hood.
- `docker-compose.yml` exposes both HTTP 6333 and gRPC 6334; ensure 6334 is reachable for the client.

References
- Semantic Kernel overview: https://learn.microsoft.com/en-us/semantic-kernel/overview/
- SK Qdrant connector: https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/qdrant-connector?pivots=programming-language-csharp
- Qdrant docs: https://qdrant.tech/documentation/

Testing
- Set `VectorDb:Provider=Qdrant` in `appsettings.json` (Console/Web)
- Run console app and submit tasks; verify no errors and embeddings are stored
- Inspect Qdrant at `http://localhost:6333` (HTTP API) for created collections named with prefix


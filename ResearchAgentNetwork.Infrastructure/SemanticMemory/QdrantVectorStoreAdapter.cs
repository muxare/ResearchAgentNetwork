using Microsoft.SemanticKernel;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using ResearchAgentNetwork.SemanticMemory;

namespace ResearchAgentNetwork.Infrastructure.SemanticMemory;

public class QdrantVectorStoreAdapter : IVectorStore
{
    private readonly Kernel _kernel;
    private readonly QdrantClient _client;
    private readonly string _collectionPrefix;

    public QdrantVectorStoreAdapter(Kernel kernel, QdrantClient client, string collectionPrefix)
    {
        _kernel = kernel;
        _client = client;
        _collectionPrefix = collectionPrefix ?? string.Empty;
    }

    private string Map(string collection) => string.IsNullOrWhiteSpace(_collectionPrefix)
        ? collection
        : $"{_collectionPrefix}_{collection}";

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken cancellationToken = default)
    {
        // Ensure collection exists with the correct vector size (lazy create)
        await EnsureCollectionAsync(Map(collection), record.Vector.Length, cancellationToken);

        var point = new PointStruct
        {
            Id = record.Id,
            Vectors = record.Vector,
        };
        if (!string.IsNullOrWhiteSpace(record.Payload))
        {
            point.Payload["payload"] = record.Payload;
        }
        // Skipping arbitrary metadata for now to keep types simple

        await _client.UpsertAsync(Map(collection), new List<PointStruct> { point }, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<VectorQueryResult>> QueryAsync(
        string collection,
        float[] vector,
        int topK,
        Dictionary<string, object>? filter = null,
        bool includePayload = false,
        CancellationToken cancellationToken = default)
    {
        var hits = await _client.SearchAsync(
            collectionName: Map(collection),
            vector: vector,
            limit: (ulong)Math.Max(1, topK),
            cancellationToken: cancellationToken);

        var results = new List<VectorQueryResult>();
        foreach (var sp in hits)
        {
            Guid id = Guid.Empty;
            try
            {
                if (!string.IsNullOrWhiteSpace(sp.Id?.Uuid) && Guid.TryParse(sp.Id.Uuid, out var gid))
                {
                    id = gid;
                }
            }
            catch { }

            string? payloadText = null;
            try
            {
                // In the current client, Payload is a MapField<string, Value>
                if (includePayload && sp.Payload != null && sp.Payload.TryGetValue("payload", out var val))
                {
                    payloadText = val.StringValue;
                }
            }
            catch { }

            results.Add(new VectorQueryResult(
                Id: id,
                Score: sp.Score,
                Payload: payloadText,
                Metadata: null));
        }
        return results;
    }

    public async Task DeleteAsync(string collection, Guid id, CancellationToken cancellationToken = default)
    {
        // No-op for now
        await Task.CompletedTask;
    }

    private async Task EnsureCollectionAsync(string collection, int vectorSize, CancellationToken ct)
    {
        var vp = new VectorParams
        {
            Size = (uint)vectorSize,
            Distance = Distance.Cosine
        };
        try
        {
            await _client.CreateCollectionAsync(collection, vp);
        }
        catch
        {
            // assume exists
        }
    }
}


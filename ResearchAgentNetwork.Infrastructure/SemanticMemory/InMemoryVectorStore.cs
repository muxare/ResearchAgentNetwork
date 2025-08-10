using System.Collections.Concurrent;
using ResearchAgentNetwork.SemanticMemory;

namespace ResearchAgentNetwork.Infrastructure.SemanticMemory;

public class InMemoryVectorStore : IVectorStore
{
    private class Record
    {
        public VectorRecord Data { get; set; } = new();
    }

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Record>> _collections = new();

    public Task UpsertAsync(string collection, VectorRecord record, CancellationToken cancellationToken = default)
    {
        var col = _collections.GetOrAdd(collection, _ => new ConcurrentDictionary<Guid, Record>());
        col[record.Id] = new Record { Data = record };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorQueryResult>> QueryAsync(
        string collection,
        float[] vector,
        int topK,
        Dictionary<string, object>? filter = null,
        bool includePayload = false,
        CancellationToken cancellationToken = default)
    {
        if (!_collections.TryGetValue(collection, out var col) || col.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<VectorQueryResult>>(Array.Empty<VectorQueryResult>());
        }

        var results = col.Values
            .Select(r => new
            {
                r.Data.Id,
                Score = CosineSimilarity(vector, r.Data.Vector),
                Payload = includePayload ? r.Data.Payload : null,
                r.Data.Metadata
            })
            .OrderByDescending(x => x.Score)
            .Take(Math.Max(1, topK))
            .Select(x => new VectorQueryResult(x.Id, x.Score, x.Payload, x.Metadata))
            .ToList();

        return Task.FromResult<IReadOnlyList<VectorQueryResult>>(results);
    }

    public Task DeleteAsync(string collection, Guid id, CancellationToken cancellationToken = default)
    {
        if (_collections.TryGetValue(collection, out var col))
        {
            col.TryRemove(id, out _);
        }
        return Task.CompletedTask;
    }

    private static double CosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        if (a.Count == 0 || b.Count == 0 || a.Count != b.Count) return 0.0;
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Count; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        if (na == 0 || nb == 0) return 0.0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}


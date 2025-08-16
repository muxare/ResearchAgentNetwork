using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;
using ResearchAgentNetwork.SemanticMemory;

namespace ResearchAgentNetwork.Infrastructure.SemanticMemory;

// Strict SK-style adapter that uses SK VectorStore abstractions under the hood
public class SkVectorStoreAdapter : IVectorStore
{
    private readonly Kernel _kernel;
    private readonly QdrantClient _client;
    private readonly string _collectionPrefix;
    private readonly int _vectorDimensions;

    // Data model for SK collections
    // Using typed records with attributes requires a fixed dimension
    public sealed class TaskRecord
    {
        [VectorStoreKey]
        public Guid Id { get; set; }

        [VectorStoreData(IsFullTextIndexed = true)]
        public string? Payload { get; set; }

        [VectorStoreVector(Dimensions: 768, DistanceFunction = DistanceFunction.CosineSimilarity)]
        public ReadOnlyMemory<float>? Embedding { get; set; }
    }

    public sealed class ResultRecord
    {
        [VectorStoreKey]
        public Guid Id { get; set; }

        [VectorStoreData(IsFullTextIndexed = true)]
        public string? Payload { get; set; }

        // Store minimal provenance in additional data columns
        [VectorStoreData]
        public string? Url { get; set; }

        [VectorStoreData]
        public int? ChunkIndex { get; set; }

        [VectorStoreData]
        public int? TotalChunks { get; set; }

        [VectorStoreVector(Dimensions: 768, DistanceFunction = DistanceFunction.CosineSimilarity)]
        public ReadOnlyMemory<float>? Embedding { get; set; }
    }

    public SkVectorStoreAdapter(Kernel kernel, QdrantClient client, string collectionPrefix, int vectorDimensions = 768)
    {
        _kernel = kernel;
        _client = client;
        _collectionPrefix = collectionPrefix ?? string.Empty;
        _vectorDimensions = Math.Max(1, vectorDimensions);
    }

    private string Map(string collection) => string.IsNullOrWhiteSpace(_collectionPrefix)
        ? collection
        : $"{_collectionPrefix}_{collection}";

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken cancellationToken = default)
    {
        var store = new QdrantVectorStore(_client, ownsClient: false);

        if (string.Equals(collection, "tasks", StringComparison.OrdinalIgnoreCase))
        {
            var col = store.GetCollection<Guid, TaskRecord>(Map(collection));
            await col.EnsureCollectionExistsAsync(cancellationToken: cancellationToken);
            // If collection exists with a different dimension, recreate lazily
            await col.UpsertAsync(new TaskRecord
            {
                Id = record.Id,
                Payload = record.Payload,
                Embedding = new ReadOnlyMemory<float>(record.Vector)
            }, cancellationToken: cancellationToken);
        }
        else
        {
            var col = store.GetCollection<Guid, ResultRecord>(Map(collection));
            await col.EnsureCollectionExistsAsync(cancellationToken: cancellationToken);
            // Map metadata fields if present
            record.Metadata ??= new Dictionary<string, object>();
            record.Metadata.TryGetValue("url", out var urlObj);
            var url = urlObj as string;
            int? chunkIndex = null;
            int? totalChunks = null;
            if (record.Metadata.TryGetValue("chunkIndex", out var ci) && ci is int cix) chunkIndex = cix;
            if (record.Metadata.TryGetValue("totalChunks", out var tc) && tc is int tcx) totalChunks = tcx;
            await col.UpsertAsync(new ResultRecord
            {
                Id = record.Id,
                Payload = record.Payload,
                Url = string.IsNullOrWhiteSpace(url) ? null : url,
                ChunkIndex = chunkIndex,
                TotalChunks = totalChunks,
                Embedding = new ReadOnlyMemory<float>(record.Vector)
            }, cancellationToken: cancellationToken);
        }
    }

    public async Task<IReadOnlyList<VectorQueryResult>> QueryAsync(
        string collection,
        float[] vector,
        int topK,
        Dictionary<string, object>? filter = null,
        bool includePayload = false,
        CancellationToken cancellationToken = default)
    {
        var store = new QdrantVectorStore(_client, ownsClient: false);
        var results = new List<VectorQueryResult>();

        if (string.Equals(collection, "tasks", StringComparison.OrdinalIgnoreCase))
        {
            var col = store.GetCollection<Guid, TaskRecord>(Map(collection));
            await foreach (var r in col.SearchAsync(new ReadOnlyMemory<float>(vector), top: Math.Max(1, topK), cancellationToken: cancellationToken))
            {
                results.Add(new VectorQueryResult(
                    Id: r.Record?.Id ?? Guid.Empty,
                    Score: r.Score ?? 0.0,
                    Payload: includePayload ? r.Record?.Payload : null,
                    Metadata: null));
            }
        }
        else
        {
            var col = store.GetCollection<Guid, ResultRecord>(Map(collection));
            await foreach (var r in col.SearchAsync(new ReadOnlyMemory<float>(vector), top: Math.Max(1, topK), cancellationToken: cancellationToken))
            {
                results.Add(new VectorQueryResult(
                    Id: r.Record?.Id ?? Guid.Empty,
                    Score: r.Score ?? 0.0,
                    Payload: includePayload ? r.Record?.Payload : null,
                    Metadata: r.Record is null ? null : new Dictionary<string, object>
                    {
                        ["url"] = r.Record.Url ?? string.Empty,
                        ["chunkIndex"] = r.Record.ChunkIndex ?? 0,
                        ["totalChunks"] = r.Record.TotalChunks ?? 0
                    }));
            }
        }

        return results;
    }

    public async Task DeleteAsync(string collection, Guid id, CancellationToken cancellationToken = default)
    {
        var store = new QdrantVectorStore(_client, ownsClient: false);
        if (string.Equals(collection, "tasks", StringComparison.OrdinalIgnoreCase))
        {
            var col = store.GetCollection<Guid, TaskRecord>(Map(collection));
            await col.DeleteAsync(id, cancellationToken: cancellationToken);
        }
        else
        {
            var col = store.GetCollection<Guid, ResultRecord>(Map(collection));
            await col.DeleteAsync(id, cancellationToken: cancellationToken);
        }
    }
}


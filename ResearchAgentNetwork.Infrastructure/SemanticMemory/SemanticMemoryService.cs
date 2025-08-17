using ResearchAgentNetwork.SemanticMemory;

namespace ResearchAgentNetwork.Infrastructure.SemanticMemory;

public class SemanticMemoryService : ISemanticMemoryService
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingService _embeddingService;

    private const string TasksCollection = "tasks";
    private const string ResultsCollection = "results";

    public SemanticMemoryService(IVectorStore vectorStore, IEmbeddingService embeddingService)
    {
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
    }

    public async Task IndexTaskAsync(ResearchTask task, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(task.Description)) return;
        var vector = await _embeddingService.EmbedAsync(task.Description, cancellationToken);
        var record = new VectorRecord
        {
            Id = task.Id,
            Vector = vector,
            Metadata = new Dictionary<string, object>
            {
                ["createdAtUtc"] = task.CreatedAt,
                ["priority"] = task.Priority,
                ["status"] = task.Status.ToString()
            },
            Payload = task.Description
        };
        await _vectorStore.UpsertAsync(TasksCollection, record, cancellationToken);
    }

    public async Task IndexResultAsync(ResearchTask task, ResearchResult result, CancellationToken cancellationToken = default)
    {
        if (result == null || string.IsNullOrWhiteSpace(result.Content)) return;
        // Chunk long content to improve retrieval granularity
        var chunks = Chunk(result.Content, maxChars: 1500, overlap: 150).ToList();
        int total = chunks.Count;
        for (int i = 0; i < total; i++)
        {
            var chunkText = chunks[i];
            var vec = await _embeddingService.EmbedAsync(chunkText, cancellationToken);
            var rec = new VectorRecord
            {
                // Generate a unique id per chunk to avoid overwriting entries in stores
                Id = Guid.NewGuid(),
                Vector = vec,
                Metadata = new Dictionary<string, object>
                {
                    ["createdAtUtc"] = DateTime.UtcNow,
                    ["confidence"] = result.ConfidenceScore,
                    ["chunkIndex"] = i,
                    ["totalChunks"] = total,
                    // Provenance fields (best-effort)
                    ["url"] = (result.Sources?.FirstOrDefault() ?? string.Empty)
                },
                Payload = chunkText
            };
            await _vectorStore.UpsertAsync(ResultsCollection, rec, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<VectorQueryResult>> RetrieveSimilarResultsAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<VectorQueryResult>();
        var queryVector = await _embeddingService.EmbedAsync(query, cancellationToken);
        // Fetch a larger candidate set to enable advanced reranking
        var initialTop = Math.Max(10, topK * 5);
        var initial = await _vectorStore.QueryAsync(ResultsCollection, queryVector, initialTop, includePayload: true, cancellationToken: cancellationToken);
        if (initial.Count == 0) return initial;

        var reranked = await RerankWithMmrAsync(queryVector, initial, topK, cancellationToken);
        return reranked;
    }

    public async Task<IReadOnlyList<VectorQueryResult>> RetrieveSimilarTasksAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<VectorQueryResult>();
        var queryVector = await _embeddingService.EmbedAsync(query, cancellationToken);
        var initialTop = Math.Max(10, topK * 5);
        var initial = await _vectorStore.QueryAsync(TasksCollection, queryVector, initialTop, includePayload: true, cancellationToken: cancellationToken);
        if (initial.Count == 0) return initial;

        var reranked = await RerankWithMmrAsync(queryVector, initial, topK, cancellationToken);
        return reranked;
    }
    private static IEnumerable<string> Chunk(string text, int maxChars, int overlap)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        maxChars = Math.Max(300, maxChars);
        overlap = Math.Clamp(overlap, 0, maxChars / 3);
        int start = 0;
        while (start < text.Length)
        {
            int len = Math.Min(maxChars, text.Length - start);
            yield return text.Substring(start, len);
            if (start + len >= text.Length) break;
            start = start + len - overlap;
            if (start < 0 || start >= text.Length) break;
        }
    }

    private static double CosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        if (a.Count != b.Count || a.Count == 0) return 0.0;
        double dot = 0.0;
        double na = 0.0;
        double nb = 0.0;
        for (int i = 0; i < a.Count; i++)
        {
            var va = a[i];
            var vb = b[i];
            dot += va * vb;
            na += va * va;
            nb += vb * vb;
        }
        if (na == 0 || nb == 0) return 0.0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    private async Task<IReadOnlyList<VectorQueryResult>> RerankWithMmrAsync(
        float[] queryVector,
        IReadOnlyList<VectorQueryResult> initial,
        int topK,
        CancellationToken ct)
    {
        // Keep only candidates that have payload text to re-embed
        var candidates = initial
            .Where(r => !string.IsNullOrWhiteSpace(r.Payload))
            .Take(Math.Min(Math.Max(10, topK * 5), 50))
            .ToList();
        if (candidates.Count == 0) return initial.Take(topK).ToList();

        var payloads = candidates.Select(c => c.Payload!).ToList();
        var embeddings = await _embeddingService.EmbedBatchAsync(payloads, ct);
        var relevance = embeddings.Select(e => CosineSimilarity(queryVector, e)).ToArray();

        // Dynamic threshold: keep items within 70% of max relevance
        var maxRel = relevance.Length > 0 ? relevance.Max() : 0.0;
        var minKeep = maxRel * 0.7;
        var filtered = new List<(int idx, double rel)>();
        for (int i = 0; i < relevance.Length; i++)
        {
            if (relevance[i] >= minKeep)
            {
                filtered.Add((i, relevance[i]));
            }
        }
        if (filtered.Count == 0) return initial.Take(topK).ToList();

        // Precompute pairwise similarities for redundancy
        var vecs = embeddings.ToArray();
        var selected = new List<int>();
        var remaining = new HashSet<int>(filtered.Select(f => f.idx));
        double alpha = 0.7; // relevance vs diversity trade-off

        while (selected.Count < topK && remaining.Count > 0)
        {
            int bestIdx = -1;
            double bestScore = double.NegativeInfinity;
            foreach (var i in remaining)
            {
                double rel = relevance[i];
                double red = 0.0;
                if (selected.Count > 0)
                {
                    foreach (var j in selected)
                    {
                        red = Math.Max(red, CosineSimilarity(vecs[i], vecs[j]));
                    }
                }
                double score = alpha * rel - (1 - alpha) * red;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx = i;
                }
            }
            if (bestIdx == -1) break;
            selected.Add(bestIdx);
            remaining.Remove(bestIdx);
        }

        // Materialize results in selected order, using our relevance as the score for consistency
        var ordered = selected
            .Select(i =>
            {
                var r = candidates[i];
                return new VectorQueryResult(r.Id, relevance[i], r.Payload, r.Metadata);
            })
            .ToList();

        return ordered;
    }
}


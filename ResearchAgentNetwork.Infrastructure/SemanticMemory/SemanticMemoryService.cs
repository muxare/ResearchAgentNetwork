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
                Id = task.Id, // same task id; chunk metadata disambiguates
                Vector = vec,
                Metadata = new Dictionary<string, object>
                {
                    ["createdAtUtc"] = DateTime.UtcNow,
                    ["confidence"] = result.ConfidenceScore,
                    ["chunkIndex"] = i,
                    ["totalChunks"] = total,
                },
                Payload = chunkText
            };
            await _vectorStore.UpsertAsync(ResultsCollection, rec, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<VectorQueryResult>> RetrieveSimilarResultsAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<VectorQueryResult>();
        var vector = await _embeddingService.EmbedAsync(query, cancellationToken);
        var results = await _vectorStore.QueryAsync(ResultsCollection, vector, topK, includePayload: true, cancellationToken: cancellationToken);
        return results;
    }

    public async Task<IReadOnlyList<VectorQueryResult>> RetrieveSimilarTasksAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<VectorQueryResult>();
        var vector = await _embeddingService.EmbedAsync(query, cancellationToken);
        var results = await _vectorStore.QueryAsync(TasksCollection, vector, topK, includePayload: true, cancellationToken: cancellationToken);
        return results;
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
}


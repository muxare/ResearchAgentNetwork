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
        var vector = await _embeddingService.EmbedAsync(result.Content, cancellationToken);
        var record = new VectorRecord
        {
            Id = task.Id,
            Vector = vector,
            Metadata = new Dictionary<string, object>
            {
                ["createdAtUtc"] = DateTime.UtcNow,
                ["confidence"] = result.ConfidenceScore
            },
            Payload = result.Content
        };
        await _vectorStore.UpsertAsync(ResultsCollection, record, cancellationToken);
    }

    public async Task<IReadOnlyList<VectorQueryResult>> RetrieveSimilarResultsAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<VectorQueryResult>();
        var vector = await _embeddingService.EmbedAsync(query, cancellationToken);
        var results = await _vectorStore.QueryAsync(ResultsCollection, vector, topK, includePayload: true, cancellationToken: cancellationToken);
        return results;
    }
}


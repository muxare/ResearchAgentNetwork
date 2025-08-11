using Microsoft.SemanticKernel;

namespace ResearchAgentNetwork.SemanticMemory;

public record VectorQueryResult(
    Guid Id,
    double Score,
    string? Payload,
    Dictionary<string, object>? Metadata
);

public class VectorRecord
{
    public Guid Id { get; set; }
    public float[] Vector { get; set; } = Array.Empty<float>();
    public Dictionary<string, object>? Metadata { get; set; }
    public string? Payload { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int? ChunkIndex { get; set; }
    public int? TotalChunks { get; set; }
}

public interface IVectorStore
{
    Task UpsertAsync(string collection, VectorRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorQueryResult>> QueryAsync(
        string collection,
        float[] vector,
        int topK,
        Dictionary<string, object>? filter = null,
        bool includePayload = false,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string collection, Guid id, CancellationToken cancellationToken = default);
}

public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> inputs, CancellationToken cancellationToken = default);
}

public interface ISemanticMemoryService
{
    Task IndexTaskAsync(ResearchTask task, CancellationToken cancellationToken = default);
    Task IndexResultAsync(ResearchTask task, ResearchResult result, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VectorQueryResult>> RetrieveSimilarResultsAsync(string query, int topK = 5, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VectorQueryResult>> RetrieveSimilarTasksAsync(string query, int topK = 5, CancellationToken cancellationToken = default);
}


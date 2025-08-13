namespace ResearchAgentNetwork;

public record RetrievedItem(
    string Kind,
    string Snippet,
    string? Title,
    string? Url,
    double? Score,
    int? ChunkIndex,
    int? TotalChunks
);


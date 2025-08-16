namespace ResearchAgentNetwork;

public class MemoryMergeSuggestion
{
    public Guid CanonicalId { get; set; }
    public List<Guid> DuplicateIds { get; set; } = new();
}


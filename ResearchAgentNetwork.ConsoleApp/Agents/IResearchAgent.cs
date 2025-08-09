using Microsoft.SemanticKernel;

namespace ResearchAgentNetwork;

public interface IResearchAgent
{
    string Role { get; }
    Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel);
}

public class AgentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}
using Microsoft.SemanticKernel;
using ResearchAgentNetwork.SemanticMemory;

namespace ResearchAgentNetwork;

public class SectionWriterAgent : IResearchAgent
{
    private readonly ISemanticMemoryService? _memory;
    public SectionWriterAgent(ISemanticMemoryService? memory)
    {
        _memory = memory;
    }

    public string Role => "SectionWriter";

    public class SectionWriteRequest
    {
        public string SectionId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public List<string> EvidenceIds { get; set; } = new();
        public string Style { get; set; } = "concise, academic, evidence-grounded";
        public string CitationStyle { get; set; } = "inline";
    }

    public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
    {
        if (!task.Metadata.TryGetValue("SectionWriteRequest", out var reqObj) || reqObj is not SectionWriteRequest req)
        {
            return new AgentResponse { Success = false, Message = "Missing SectionWriteRequest" };
        }

        // Retrieve evidence text for context (best-effort)
        var evidenceText = string.Empty;
        if (_memory != null && req.EvidenceIds.Count > 0)
        {
            // Current memory interface does not expose retrieval by ids; fallback is to concatenate ids and rely on model
            evidenceText = string.Join("\n\n", req.EvidenceIds.Select(id => $"[EVIDENCE:{id}]"));
        }

        var prompt = $@"Write the '{req.Title}' section for the research report.
Use ONLY the provided evidence. Do not invent facts. Include inline citations like [n] tied to evidence items.
Style: {req.Style}. CitationStyle: {req.CitationStyle}.

Evidence:
{evidenceText}

Return ONLY valid JSON with fields: SectionId, Title, ContentMd, Citations (array of strings), FactsUsed (array of strings), Confidence (number).";

        var draft = await kernel.WithStructuredOutputRetry<ReportSectionDraft>(prompt);
        task.Metadata[$"SectionDraft:{req.SectionId}"] = draft;
        return new AgentResponse { Success = true, Data = draft };
    }
}


# Research Agent Network

A multi-agent research assistant built on Microsoft Semantic Kernel. It decomposes complex research tasks, executes atomic subtasks with an LLM, assesses quality, and aggregates results into a final synthesis.

## Requirements
- .NET 9 SDK
- One LLM provider:
  - Ollama (default, local) or
  - OpenAI or Azure OpenAI

## Quickstart

1) Clone and build
```bash
git clone <repo>
cd ResearchAgentNetwork
dotnet build
```

2) Configure provider (no code changes needed)
- appsettings.json (default is Ollama)
```json
{
  "AI_PROVIDER": "Ollama",
  "Ollama": {
    "ModelId": "llama3.1:latest",
    "Endpoint": "http://localhost:11434",
    "EmbeddingModelId": "llama3.1"
  },
  "ResearchAgent": {
    "MaxConcurrency": 5,
    "DefaultPriority": 5,
    "TaskTimeoutMinutes": 30
  }
}
```
- or environment variables
```
AI_PROVIDER=Ollama
Ollama__ModelId=llama3.1:latest
Ollama__Endpoint=http://localhost:11434
Ollama__EmbeddingModelId=llama3.1
```

3) Run the console app
```bash
dotnet run --project ResearchAgentNetwork.ConsoleApp
```
You’ll see logs for:
- task processing
- decomposition (subtasks enqueued)
- execution completion (confidence)
- aggregation for parent tasks

4) Sample complex task (pre-configured in `Program.cs`)
- “Analyze the impact of quantum computing on cryptography, including current vulnerabilities, post-quantum algorithms, and migration strategies for enterprises.”

## Tests
```bash
dotnet test
```
- LLM integration tests are resilient and will skip if the local Ollama service is unavailable.

## What’s implemented (LLM-only)
- Structured outputs across agents with JSON schema guidance
- Robust decomposition with fallbacks
- Execution with structured `content` and claimed `sources`
- Quality assessment and follow-up generation
- Parent aggregation once all children complete

## Roadmap
- Web search integration via `IWebSearchService`
- Embedding-based similarity and task merging
- Persistence (EF Core) and result caching
- Web API + UI

## More docs
- docs/ResearchAgentNetwork-Implementation.md
- docs/SimpleResearchAssistant-Plan.md
- docs/SimpleResearchAssistant-Phase2.md
- docs/SimpleResearchAssistant-Phase3.md
- docs/SimpleResearchAssistant-Phase4.md
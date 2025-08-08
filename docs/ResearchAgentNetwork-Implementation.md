# Research Agent Network - Implementation Documentation

## Overview

The Research Agent Network is a sophisticated multi-agent system built with Microsoft Semantic Kernel that orchestrates complex research tasks through decomposition, execution, and synthesis. The system uses specialized agents to break down complex research questions into manageable subtasks, execute them in parallel, and aggregate results into comprehensive findings.

## Architecture

### Core Components

#### 1. Domain Models
- **`ResearchTask`**: Represents a research task with metadata, status tracking, and hierarchical relationships
- **`ResearchResult`**: Contains research findings with confidence scores, sources, and metadata
- **`TaskStatus`**: Enumeration of task states (Pending, Analyzing, Executing, Aggregating, Completed, Failed)

#### 2. Agent System
The system implements a multi-agent architecture with specialized roles:

- **`TaskAnalyzerAgent`**: Analyzes task complexity and decomposes complex tasks into subtasks
- **`TaskMergerAgent`**: Identifies and merges similar tasks to avoid duplication
- **`ExecutorAgent`**: Executes atomic research tasks using LLM capabilities
- **`AggregatorAgent`**: Synthesizes results from multiple subtasks into coherent reports
- **`QualityAssessmentAgent`**: Evaluates result quality and generates follow-up tasks

#### 3. Orchestration
- **`ResearchOrchestrator`**: Manages the entire research workflow, task queuing, and agent coordination

## Data Flow

### 1. Task Submission
```
User Input → ResearchOrchestrator.SubmitResearchTask() → Task Queue → Task Registry
```

### 2. Task Processing Pipeline
```
Task Queue → TaskAnalyzerAgent → [Decomposition or Execution] → TaskMergerAgent → ExecutorAgent → QualityAssessmentAgent → AggregatorAgent
```

### 3. Result Flow
```
ExecutorAgent → ResearchResult → QualityAssessmentAgent → [Follow-up Tasks or Completion] → AggregatorAgent → Final Synthesis
```

## Execution Flow

### Phase 1: Task Analysis
1. **Complexity Assessment**: The `TaskAnalyzerAgent` evaluates task complexity using LLM analysis
2. **Decomposition Decision**: Based on complexity, tasks are either decomposed into subtasks or marked for direct execution
3. **Subtask Creation**: Complex tasks are broken down into 3-5 focused subtasks

### Phase 2: Task Optimization
1. **Similarity Detection**: The `TaskMergerAgent` identifies semantically similar tasks
2. **Task Merging**: Similar tasks are merged to avoid redundant work
3. **Queue Management**: Tasks are prioritized and queued for execution

### Phase 3: Task Execution
1. **Atomicity Check**: The `ExecutorAgent` verifies if tasks are atomic enough for direct execution
2. **Context Building**: Research context is assembled from related tasks and metadata
3. **LLM Execution**: Tasks are executed using Semantic Kernel's LLM capabilities
4. **Quality Evaluation**: Results are assessed for comprehensiveness and accuracy

### Phase 4: Result Synthesis
1. **Quality Assessment**: The `QualityAssessmentAgent` evaluates result quality
2. **Gap Analysis**: Identifies areas requiring additional research
3. **Follow-up Generation**: Creates additional tasks to address gaps
4. **Result Aggregation**: The `AggregatorAgent` synthesizes multiple results into coherent reports

### Phase 5: Parent Task Completion
1. **Dependency Tracking**: Monitors completion of all subtasks
2. **Parent Aggregation**: Triggers aggregation when all subtasks are complete
3. **Final Synthesis**: Creates comprehensive final reports

## Key Features

### 1. Hierarchical Task Management
- Parent-child relationships between tasks
- Automatic dependency tracking
- Cascading completion notifications

### 2. Concurrent Execution
- Parallel task processing with configurable concurrency limits
- Thread-safe task queue and registry
- Semaphore-based throttling

### 3. Quality Assurance
- Multi-stage quality assessment
- Confidence scoring
- Automatic gap detection and follow-up task generation

### 4. Semantic Similarity
- Embedding-based task similarity detection
- Duplicate task prevention
- Intelligent task merging

## Testing Instructions

### Prerequisites
1. .NET 9.0 SDK
2. OpenAI API key (or compatible LLM provider)
3. Valid API credentials

### Setup
1. **Clone and Build**:
   ```bash
   git clone <repository>
   cd ResearchAgentNetwork
   dotnet build
   ```

2. **Configure Provider (no code changes needed)**:
   The app uses `AIProviderFactory` and reads provider settings from `appsettings.json` or environment variables.

   - `appsettings.json` example:
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

   - Environment variables override config:
     ```
     AI_PROVIDER=Ollama
     Ollama__ModelId=llama3.1:latest
     Ollama__Endpoint=http://localhost:11434
     Ollama__EmbeddingModelId=llama3.1
     ```

### Test Scenarios

#### 1. Basic Research Task
```csharp
var taskId = await orchestrator.SubmitResearchTask(
    "What are the latest developments in quantum computing?"
);
```

#### 2. Complex Multi-Faceted Research
```csharp
var taskId = await orchestrator.SubmitResearchTask(
    "Analyze the impact of quantum computing on cryptography, " +
    "including current vulnerabilities, post-quantum algorithms, " +
    "and migration strategies for enterprises"
);
```

#### 3. Monitoring Progress
```csharp
while (true)
{
    var status = orchestrator.GetTaskStatus(taskId);
    Console.WriteLine($"Task {taskId}: {status?.Status}");
    
    if (status?.Status == TaskStatus.Completed)
    {
        Console.WriteLine($"Result: {status.Result?.Content}");
        break;
    }
    
    await Task.Delay(1000);
}
```

### Expected Behaviors

1. **Simple Tasks**: Should execute directly without decomposition
2. **Complex Tasks**: Should be decomposed into 3-5 subtasks
3. **Quality Assessment**: Should generate follow-up tasks for incomplete research
4. **Aggregation**: Parent tasks synthesize completed child results into a coherent report

### Debugging

1. **Check Task Status**: Monitor task progression through different states
2. **Review Agent Responses**: Each agent returns `AgentResponse` with success/failure information
3. **Examine Results**: Check `ResearchResult` content, confidence scores, and sources
4. **Monitor Logs**: Watch for error messages and task failures

## Configuration Options

### Concurrency Control
```csharp
var orchestrator = new ResearchOrchestrator(kernel, maxConcurrency: 5);
```

### Task Priority
```csharp
var taskId = await orchestrator.SubmitResearchTask(description, priority: 10);
```

### Custom Agents
Implement `IResearchAgent` interface to create custom specialized agents.

### Provider Selection
- `AIProviderFactory` supports `Ollama`, `OpenAI`, and `AzureOpenAI`.
- Embeddings are configured per provider to enable future similarity features.

### Structured Outputs
- All LLM interactions use `KernelExtensions.WithStructuredOutputRetry<T>` with JSON schema guidance.
- The system tolerates minor formatting issues (e.g., fenced code blocks) when parsing JSON.

## Limitations and Future Enhancements

### Current Limitations
1. **Embedding Implementation**: Semantic similarity detection is currently stubbed out
2. **Error Handling**: Basic error handling with task failure states
3. **Persistence**: In-memory storage only (no database persistence)
4. **API Dependencies**: Requires OpenAI API access

### Planned Enhancements
1. **Database Integration**: Persistent task and result storage
2. **Advanced Embeddings**: Full semantic similarity implementation
3. **Web Interface**: REST API and web dashboard
4. **Plugin System**: Extensible agent architecture
5. **Result Caching**: Avoid redundant research
6. **Multi-Provider Support**: Support for multiple LLM providers

## Performance Considerations

1. **Concurrency Limits**: Adjust based on API rate limits and system resources
2. **Task Size**: Balance between task granularity and overhead
3. **Memory Usage**: Monitor task registry size for long-running sessions
4. **API Costs**: Consider token usage and API call frequency

## Security Considerations

1. **API Key Management**: Use secure configuration management
2. **Input Validation**: Validate task descriptions and metadata
3. **Output Sanitization**: Sanitize LLM outputs before processing
4. **Access Control**: Implement proper authentication for production use 
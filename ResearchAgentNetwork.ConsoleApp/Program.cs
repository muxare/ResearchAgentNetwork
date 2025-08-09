using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using ResearchAgentNetwork.AIProviders;
using NJsonSchema.Generation;
using NJsonSchema;
using Newtonsoft.Json.Schema;

namespace ResearchAgentNetwork
{
    // Core domain models
    public class ResearchTask
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Description { get; set; } = string.Empty;
        public TaskStatus Status { get; set; } = TaskStatus.Pending;
        public int Priority { get; set; }
        public Guid? ParentTaskId { get; set; }
        public List<Guid> SubTaskIds { get; set; } = new();
        public ResearchResult? Result { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum TaskStatus
    {
        Pending, Analyzing, Executing, Aggregating, Completed, Failed
    }

    public class ResearchResult
    {
        public string Content { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public List<string> Sources { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool RequiresAdditionalResearch { get; set; }
    }

    public class TaskEvent
    {
        public Guid TaskId { get; set; }
        public TaskStatus Status { get; set; }
        public string EventType { get; set; } = string.Empty; // submitted, status, decomposed, aggregated, completed, failed
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string? Message { get; set; }
        public Guid? ParentTaskId { get; set; }
    }

    public class ComplexityAnalysis
    {
        public bool RequiresDecomposition { get; set; }
        public int Complexity { get; set; }
        public string Reasoning { get; set; } = string.Empty;
    }

    // Agent interfaces
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

    // Specialized agents
    public class TaskAnalyzerAgent : IResearchAgent
    {
        public string Role => "TaskAnalyzer";

        public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
        {
            // Analyze task complexity using embeddings and heuristics
            var complexity = await AnalyzeComplexity(task, kernel);
            // Store analysis for reporting
            task.Metadata["ComplexityAnalysis"] = complexity;

            if (complexity.RequiresDecomposition)
            {
                var subTasks = await DecomposeTask(task, kernel);
                return new AgentResponse
                {
                    Success = true,
                    Data = subTasks
                };
            }

            return new AgentResponse
            {
                Success = true,
                Data = new { ReadyForExecution = true }
            };
        }

        private async Task<List<ResearchTask>> DecomposeTask(ResearchTask task, Kernel kernel)
        {
            var prompt = $@"Decompose this research task into 3-5 focused subtasks that can be researched independently.
Task: {task.Description}

Return only an array of subtask descriptions.";

            try
            {
                var descriptions = await kernel.WithStructuredOutputRetry<List<string>>(prompt);
                task.Metadata["Decomposition"] = descriptions;
                return descriptions.Select(d => new ResearchTask { Description = d }).ToList();
            }
            catch
            {
                // Fallback: accept array of objects { "title": "description" } and flatten values
                var fallbackPrompt = $@"Decompose this research task into 3-5 focused subtasks that can be researched independently.
Task: {task.Description}

Return only an array of objects where each object has a single property mapping subtask title to its description.";

                var objects = await kernel.WithStructuredOutputRetry<List<Dictionary<string, string>>>(fallbackPrompt);
                var descriptions = objects.SelectMany(o => o.Values).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                if (descriptions.Count == 0)
                {
                    // Last resort: return the original task as a single subtask to avoid stalling
                    descriptions.Add(task.Description);
                }
                task.Metadata["Decomposition"] = descriptions;
                return descriptions.Select(d => new ResearchTask { Description = d }).ToList();
            }
        }

        private async Task<ComplexityAnalysis> AnalyzeComplexity(ResearchTask task, Kernel kernel)
        {
            var prompt = $@"Analyze this research task complexity and whether it requires decomposition.
Task: {task.Description}

Consider:
1. Number of distinct sub-topics
2. Depth of analysis required
3. Domain expertise needed";

            var analysis = await kernel.WithStructuredOutputRetry<ComplexityAnalysis>(prompt);
            return analysis;
        }
    }

    public class TaskMergerAgent : IResearchAgent
    {
        public string Role => "TaskMerger";

        public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
        {
            // Find similar tasks using semantic similarity
            var similarTasks = await FindSimilarTasks(task);

            if (similarTasks.Count > 0)
            {
                var mergedTask = await MergeTasks(task, similarTasks, kernel);
                return new AgentResponse
                {
                    Success = true,
                    Data = mergedTask
                };
            }

            return new AgentResponse { Success = true };
        }

        private Task<List<ResearchTask>> FindSimilarTasks(ResearchTask task)
        {
            // This could also be solved by asking an llm to find similar tasks

            // Use embeddings to find semantically similar tasks
            // var embedding = await _embeddingService.GenerateEmbeddingAsync(task.Description);
            // Compare with other pending tasks...
            return Task.FromResult(new List<ResearchTask>());
        }

        private async Task<ResearchTask> MergeTasks(ResearchTask task, List<ResearchTask> similarTasks, Kernel kernel)
        {
            var schema = JsonStuff.GenerateJsonSchemaFromClass<ResearchTask>();
            var prompt = $@"Merge these similar research tasks:
                Main task: {task.Description}
                Similar tasks: {string.Join(", ", similarTasks.Select(t => t.Description))}

                Create a unified task description that covers all aspects and return as JSON: {schema}.";

            var result = await kernel.InvokePromptAsync(prompt);
            var mergedDescription = result.GetValue<string>() ?? task.Description;

            return new ResearchTask { Description = mergedDescription };
        }
    }

    public class ExecutorAgent : IResearchAgent
    {
        public string Role => "Executor";

        public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
        {
            // Force execute if orchestrator requested it (e.g., max depth reached)
            if (task.Metadata.TryGetValue("ForceExecute", out var forceObj) && forceObj is bool force && force)
            {
                var forced = await ExecuteWithLLM(task, kernel);
                return new AgentResponse { Success = true, Data = forced };
            }

            // Check if task is atomic enough for LLM execution
            if (await IsAtomicTask(task, kernel))
            {
                var result = await ExecuteWithLLM(task, kernel);
                return new AgentResponse
                {
                    Success = true,
                    Data = result
                };
            }

            return new AgentResponse
            {
                Success = false,
                Message = "Task not atomic enough for execution"
            };
        }

        private async Task<ResearchResult> ExecuteWithLLM(ResearchTask task, Kernel kernel)
        {
            var context = BuildContext(task);
            var prompt = $@"Research the following topic thoroughly and produce a structured result.
Topic: {task.Description}

Context: {context}

Return fields for content (string) and sources (array of strings with URLs or citations).";

            var structured = await kernel.WithStructuredOutputRetry<ExecutorStructuredResult>(prompt);

            var content = structured.Content ?? string.Empty;
            var sources = structured.Sources?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();

            var quality = await EvaluateQuality(content, kernel);
            var completeness = await CheckCompleteness(content, task, kernel);

            // Store for reporting
            task.Metadata["QualityScore"] = quality;
            task.Metadata["Completeness"] = new Completeness { NeedsMoreResearch = completeness, Reasoning = string.Empty };

            return new ResearchResult
            {
                Content = content,
                Sources = sources,
                ConfidenceScore = quality,
                RequiresAdditionalResearch = completeness
            };
        }

        private string BuildContext(ResearchTask task)
        {
            // Build context from related tasks and metadata
            return $"Research context for: {task.Description}";
        }

        private async Task<double> EvaluateQuality(string content, Kernel kernel)
        {
            var prompt = $@"Rate the quality of this research content from 0.0 to 1.0.
Content:
{content}

Consider comprehensiveness, accuracy, and relevance.";

            var quality = await kernel.WithStructuredOutputRetry<QualityScore>(prompt);
            return quality.Score;
        }

        public class Completeness
        {
            public bool NeedsMoreResearch { get; set; }
            public string Reasoning { get; set; } = string.Empty;
        }

        public class AtomicityCheck
        {
            public bool IsAtomic { get; set; }
            public string Reasoning { get; set; } = string.Empty;
        }

        public class QualityScore
        {
            public double Score { get; set; }
        }

        public class ExecutorStructuredResult
        {
            public string Content { get; set; } = string.Empty;
            public List<string>? Sources { get; set; }
        }

        private async Task<bool> CheckCompleteness(string content, ResearchTask task, Kernel kernel)
        {
            var prompt = $@"Assess whether this research content requires additional investigation.
Task: {task.Description}
Content:
{content}";

            var completeness = await kernel.WithStructuredOutputRetry<Completeness>(prompt);
            return completeness.NeedsMoreResearch;
        }

        private async Task<bool> IsAtomicTask(ResearchTask task, Kernel kernel)
        {
            var prompt = $@"Is this research task atomic enough for direct execution?
Task: {task.Description}

Consider if it's focused, specific, and can be answered directly.";

            var atomicity = await kernel.WithStructuredOutputRetry<AtomicityCheck>(prompt);
            return atomicity.IsAtomic;
        }
    }

    public class AggregatorAgent : IResearchAgent
    {
        public string Role => "Aggregator";
        private readonly Func<Guid, List<ResearchTask>> _getChildren;

        public AggregatorAgent(Func<Guid, List<ResearchTask>> getChildren)
        {
            _getChildren = getChildren;
        }

        public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
        {
            var subTaskResults = await GetSubTaskResults(task);

            // Allow aggregation when all children have reached a terminal state (Completed or Failed)
            if (subTaskResults.All(r => r.Status == TaskStatus.Completed || r.Status == TaskStatus.Failed))
            {
                var aggregatedResult = await AggregateResults(subTaskResults, task, kernel);
                return new AgentResponse
                {
                    Success = true,
                    Data = aggregatedResult
                };
            }

            return new AgentResponse
            {
                Success = false,
                Message = "Not all subtasks completed"
            };
        }

        private Task<List<ResearchTask>> GetSubTaskResults(ResearchTask task)
        {
            var children = task.Id == Guid.Empty ? new List<ResearchTask>() : _getChildren(task.Id);
            return Task.FromResult(children);
        }

        private async Task<ResearchResult> AggregateResults(
            List<ResearchTask> subTasks,
            ResearchTask parentTask,
            Kernel kernel)
        {
            var completed = subTasks
                .Where(s => s.Status == TaskStatus.Completed && s.Result != null)
                .ToList();
            var failed = subTasks.Where(s => s.Status == TaskStatus.Failed).ToList();

            // Token-bounded child summaries (simple truncation)
            string Summarize(ResearchTask t)
            {
                var content = t.Result?.Content ?? string.Empty;
                var max = 800; // characters
                if (content.Length > max) content = content.Substring(0, max) + "...";
                return $"- {t.Description}:\n{content}";
            }

            var summaryBlock = string.Join("\n\n", completed.Select(Summarize));
            var failedBlock = failed.Any()
                ? ("The following subtasks failed and should be treated cautiously in synthesis: " + string.Join(", ", failed.Select(f => f.Description)))
                : "";

            var prompt = $@"You will synthesize multiple child research findings into a coherent report.
Parent task: {parentTask.Description}

Child summaries:
{summaryBlock}

{failedBlock}

Return a JSON object with fields: content (string, the final synthesized report) and sources (array of strings with URLs or citations, deduplicated).";

            var structured = await kernel.WithStructuredOutputRetry<ExecutorAgent.ExecutorStructuredResult>(prompt);

            var childSources = completed.SelectMany(t => t.Result?.Sources ?? new()).Distinct().ToList();
            var sources = (structured.Sources != null && structured.Sources.Any()) ? structured.Sources : childSources;

            return new ResearchResult
            {
                Content = structured.Content ?? string.Empty,
                ConfidenceScore = CalculateAggregateConfidence(completed),
                Sources = sources
            };
        }

        private double CalculateAggregateConfidence(List<ResearchTask> subTasks)
        {
            if (!subTasks.Any()) return 0;
            return subTasks.Average(t => t.Result?.ConfidenceScore ?? 0);
        }
    }

    public class QualityAssessmentAgent : IResearchAgent
    {
        public string Role => "QualityAssessor";

        public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
        {
            if (task.Result == null) return new AgentResponse { Success = false };

            // TODO: I think this would have to check stored knowledge and not stored knowledge
            var assessment = await AssessResultQuality(task.Result, task, kernel);
            task.Metadata["QualityAssessment"] = assessment;

            if (assessment.NeedsMoreResearch)
            {
                var additionalTasks = await GenerateFollowUpTasks(assessment, task, kernel);
                return new AgentResponse
                {
                    Success = true,
                    Data = additionalTasks
                };
            }

            return new AgentResponse { Success = true };
        }

        private async Task<List<ResearchTask>> GenerateFollowUpTasks(QualityAssessment assessment, ResearchTask task, Kernel kernel)
        {
            var prompt = $@"Generate follow-up research tasks to address these gaps:
                Original task: {task.Description}
                Gaps identified: {string.Join(", ", assessment.Gaps)}
                
                Return JSON array of task descriptions.";

            var result = await kernel.InvokePromptAsync(prompt);
            var jsonResult = result.GetValue<string>() ?? "[]";
            var descriptions = JsonSerializer.Deserialize<List<string>>(jsonResult) ?? new List<string>();

            return descriptions.Select(d => new ResearchTask { Description = d }).ToList();
        }

        private async Task<QualityAssessment> AssessResultQuality(ResearchResult result, ResearchTask task, Kernel kernel)
        {
            var prompt = $@"Assess the quality of this research result:
                Task: {task.Description}
                Result: {result.Content}
                
                Return JSON: {{needsMoreResearch: bool, reasoning: string, gaps: string[]}}";

            var result2 = await kernel.InvokePromptAsync(prompt);
            var jsonResult = result2.GetValue<string>() ?? "{}";
            return JsonSerializer.Deserialize<QualityAssessment>(jsonResult) ?? new QualityAssessment();
        }

    }

    public class QualityAssessment
    {
        public bool NeedsMoreResearch { get; set; }
        public string Reasoning { get; set; } = "";
        public List<string> Gaps { get; set; } = new();
    }

    // Orchestrator
    public class ResearchOrchestrator
    {
        private readonly ConcurrentQueue<ResearchTask> _taskQueue = new();
        private readonly ConcurrentDictionary<Guid, ResearchTask> _taskRegistry = new();
        private readonly Dictionary<string, IResearchAgent> _agents = new();
        private readonly Kernel _kernel;
        private readonly SemaphoreSlim _throttle;
        private int _maxDecompositionDepth;
        private int _processorStarted = 0;

        public ResearchOrchestrator(Kernel kernel, int maxConcurrency = 5, int maxDecompositionDepth = 2)
        {
            _kernel = kernel;
            _throttle = new SemaphoreSlim(maxConcurrency);
            _maxDecompositionDepth = Math.Max(0, maxDecompositionDepth);
            InitializeAgents();
        }

        public event Action<TaskEvent>? TaskEventPublished;

        private void Publish(TaskEvent e)
        {
            try { TaskEventPublished?.Invoke(e); } catch { /* swallow */ }
        }

        public void UpdateMaxDecompositionDepth(int maxDepth)
        {
            _maxDecompositionDepth = Math.Max(0, maxDepth);
        }

        private void InitializeAgents()
        {
            _agents["analyzer"] = new TaskAnalyzerAgent();
            _agents["merger"] = new TaskMergerAgent();
            _agents["executor"] = new ExecutorAgent();
            _agents["aggregator"] = new AggregatorAgent((Guid parentId) =>
            {
                return _taskRegistry.Values
                    .Where(t => t.ParentTaskId == parentId)
                    .ToList();
            });
            _agents["assessor"] = new QualityAssessmentAgent();
        }

        public async Task<Guid> SubmitResearchTask(string description, int priority = 5)
        {
            var task = new ResearchTask
            {
                Description = description,
                Priority = priority
            };

            _taskQueue.Enqueue(task);
            _taskRegistry[task.Id] = task;
            Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "submitted" });

            // Start processing if not already running
            if (Interlocked.Exchange(ref _processorStarted, 1) == 0)
            {
                _ = Task.Run(() => ProcessTasksAsync());
            }

            return task.Id;
        }

        private async Task ProcessTasksAsync()
        {
            while (true)
            {
                if (_taskQueue.TryDequeue(out var task))
                {
                    await _throttle.WaitAsync();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessSingleTask(task);
                        }
                        finally
                        {
                            _throttle.Release();
                        }
                    });
                }
                else
                {
                    await Task.Delay(100);
                }
            }
        }

        private async Task ProcessSingleTask(ResearchTask task)
        {
            try
            {
                // Respect cancellation
                if (task.Metadata.TryGetValue("Cancelled", out var c) && c is bool cval && cval)
                {
                    task.Status = TaskStatus.Failed;
                    task.Result = new ResearchResult { Content = "Cancelled", ConfidenceScore = 0 };
                    Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "failed", Message = "Cancelled" });
                    return;
                }
                Console.WriteLine($"➡️  Processing task {task.Id} (priority {task.Priority}): {task.Description}");
                if (task.Status == TaskStatus.Aggregating)
                {
                    // Aggregate child results into parent
                    var aggregatorResponse = await _agents["aggregator"].ProcessAsync(task, _kernel);
                    if (aggregatorResponse.Success && aggregatorResponse.Data is ResearchResult aggResult)
                    {
                        task.Result = aggResult;
                        task.Status = TaskStatus.Completed;
                        Console.WriteLine($"🧷 Aggregated {task.SubTaskIds.Count} subtasks for parent {task.Id}");
                        Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "aggregated" });
                    }
                    return;
                }

                // Respect max decomposition depth: skip analysis/merge if depth limit reached
                var currentDepth = ComputeTaskDepth(task);
                if (currentDepth < _maxDecompositionDepth)
                {
                    task.Status = TaskStatus.Analyzing;
                    Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "status" });

                    // 1. Analyze for decomposition
                    var analyzerResponse = await _agents["analyzer"].ProcessAsync(task, _kernel);
                    if (analyzerResponse.Data is List<ResearchTask> subTasks)
                    {
                        Console.WriteLine($"🧩 Decomposed into {subTasks.Count} subtasks (depth {currentDepth} → {currentDepth + 1})");
                        foreach (var subTask in subTasks)
                        {
                            subTask.ParentTaskId = task.Id;
                            task.SubTaskIds.Add(subTask.Id);
                            _taskQueue.Enqueue(subTask);
                            _taskRegistry[subTask.Id] = subTask;
                            Console.WriteLine($"  ↳ Enqueued subtask {subTask.Id}: {subTask.Description}");
                            Publish(new TaskEvent { TaskId = subTask.Id, Status = subTask.Status, EventType = "submitted", ParentTaskId = task.Id });
                        }
                        // Mark parent as awaiting children completion
                        task.Status = TaskStatus.Pending;
                        Console.WriteLine($"⏸️  Waiting for {subTasks.Count} subtasks to complete before aggregation");
                        Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "decomposed", Message = $"{subTasks.Count} subtasks" });
                        return;
                    }

                    // 2. Check for merge opportunities
                    var mergerResponse = await _agents["merger"].ProcessAsync(task, _kernel);
                    if (mergerResponse.Data is ResearchTask mergedTask)
                    {
                        // Update task references
                        task = mergedTask;
                    }
                }
                else
                {
                    Console.WriteLine($"🔚 Max decomposition depth {_maxDecompositionDepth} reached (current depth {currentDepth}). Executing directly.");
                    task.Metadata["ForceExecute"] = true;
                }

                // 3. Execute if atomic
                task.Status = TaskStatus.Executing;
                Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "status" });
                var executorResponse = await _agents["executor"].ProcessAsync(task, _kernel);
                if (executorResponse.Success && executorResponse.Data is ResearchResult result)
                {
                    task.Result = result;
                    task.Status = TaskStatus.Completed;
                    Console.WriteLine($"✅ Completed task {task.Id} with confidence {task.Result.ConfidenceScore:P1}");
                    Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "completed" });

                    // 4. Check if parent needs aggregation
                    if (task.ParentTaskId.HasValue)
                    {
                        await CheckParentAggregation(task.ParentTaskId.Value);
                    }

                    // 5. Assess quality and generate follow-ups if needed
                    var assessorResponse = await _agents["assessor"].ProcessAsync(task, _kernel);
                    if (assessorResponse.Data is List<ResearchTask> followUpTasks)
                    {
                        foreach (var followUp in followUpTasks)
                        {
                            followUp.ParentTaskId = task.Id;
                            _taskQueue.Enqueue(followUp);
                            _taskRegistry[followUp.Id] = followUp;
                        }
                    }
                }
                else
                {
                    // If executor declined due to non-atomic, fallback: try forced execution once
                    var attempts = 0;
                    if (task.Metadata.TryGetValue("ExecAttempts", out var att) && att is int a) attempts = a;
                    task.Metadata["ExecAttempts"] = attempts + 1;

                    if (currentDepth >= _maxDecompositionDepth || attempts >= 1)
                    {
                        Console.WriteLine($"⚙️ Forcing execution for task {task.Id} (attempt {attempts + 1}).");
                        task.Metadata["ForceExecute"] = true;
                        var forced = await _agents["executor"].ProcessAsync(task, _kernel);
                        if (forced.Success && forced.Data is ResearchResult forcedResult)
                        {
                            task.Result = forcedResult;
                            task.Status = TaskStatus.Completed;
                            Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "completed" });
                        }
                        else
                        {
                            task.Status = TaskStatus.Failed;
                            Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "failed", Message = "Forced execution failed" });
                        }
                    }
                    else
                    {
                        // Re-analyze path
                        task.Status = TaskStatus.Analyzing;
                        Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "status" });
                        _taskQueue.Enqueue(task);
                    }
                }
            }
            catch (Exception ex)
            {
                task.Status = TaskStatus.Failed;
                task.Result = new ResearchResult
                {
                    Content = $"Error: {ex.Message}",
                    ConfidenceScore = 0
                };
                Console.WriteLine($"❌ Task {task.Id} failed: {ex.Message}");
                Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "failed", Message = ex.Message });
            }
        }

        private int ComputeTaskDepth(ResearchTask task)
        {
            int depth = 0;
            var current = task;
            while (current.ParentTaskId.HasValue && _taskRegistry.TryGetValue(current.ParentTaskId.Value, out var parent))
            {
                depth++;
                current = parent;
            }
            return depth;
        }

        private async Task CheckParentAggregation(Guid parentId)
        {
            if (_taskRegistry.TryGetValue(parentId, out var parentTask))
            {
                var subTasks = _taskRegistry.Values
                    .Where(t => t.ParentTaskId == parentId)
                    .ToList();

                if (subTasks.All(t => t.Status == TaskStatus.Completed))
                {
                    parentTask.Status = TaskStatus.Aggregating;
                    _taskQueue.Enqueue(parentTask);
                    Publish(new TaskEvent { TaskId = parentTask.Id, Status = parentTask.Status, EventType = "status" });
                }
            }
        }

        // Progress/reporting helpers
        public IEnumerable<ResearchTask> GetAllTasks() => _taskRegistry.Values.ToList();

        public List<ResearchTask> GetChildren(Guid taskId)
        {
            return _taskRegistry.Values.Where(t => t.ParentTaskId == taskId).ToList();
        }

        public bool CancelTask(Guid taskId)
        {
            if (_taskRegistry.TryGetValue(taskId, out var task))
            {
                task.Metadata["Cancelled"] = true;
                return true;
            }
            return false;
        }

        public bool RetryTask(Guid taskId)
        {
            if (_taskRegistry.TryGetValue(taskId, out var task))
            {
                task.Status = TaskStatus.Pending;
                task.Result = null;
                task.Metadata.Remove("ExecAttempts");
                task.Metadata.Remove("ForceExecute");
                _taskQueue.Enqueue(task);
                Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "status", Message = "Retry" });
                return true;
            }
            return false;
        }

        public bool ForceExecute(Guid taskId)
        {
            if (_taskRegistry.TryGetValue(taskId, out var task))
            {
                task.Metadata["ForceExecute"] = true;
                _taskQueue.Enqueue(task);
                Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "status", Message = "ForceExecute" });
                return true;
            }
            return false;
        }

        public Dictionary<TaskStatus, int> GetProgressSummary()
        {
            return _taskRegistry.Values
                .GroupBy(t => t.Status)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public string GenerateTaskReport(Guid taskId)
        {
            if (!_taskRegistry.TryGetValue(taskId, out var task)) return "Task not found.";
            var sb = new StringBuilder();
            sb.AppendLine($"# Report: {task.Description}");
            sb.AppendLine($"- Id: {task.Id}");
            sb.AppendLine($"- Status: {task.Status}");
            sb.AppendLine($"- Priority: {task.Priority}");
            sb.AppendLine($"- Created: {task.CreatedAt:u}");
            sb.AppendLine();

            if (task.Metadata.TryGetValue("ComplexityAnalysis", out var ca) && ca is ComplexityAnalysis comp)
            {
                sb.AppendLine("## Complexity Analysis");
                sb.AppendLine($"- Requires Decomposition: {comp.RequiresDecomposition}");
                sb.AppendLine($"- Complexity: {comp.Complexity}");
                sb.AppendLine($"- Reasoning: {comp.Reasoning}");
                sb.AppendLine();
            }

            if (task.Metadata.TryGetValue("Decomposition", out var decomp) && decomp is List<string> parts && parts.Count > 0)
            {
                sb.AppendLine("## Decomposition");
                foreach (var p in parts) sb.AppendLine($"- {p}");
                sb.AppendLine();
            }

            if (task.Result != null)
            {
                sb.AppendLine("## Result");
                sb.AppendLine($"Confidence: {task.Result.ConfidenceScore:P1}");
                sb.AppendLine($"Requires Additional Research: {task.Result.RequiresAdditionalResearch}");
                if (task.Result.Sources.Any())
                {
                    sb.AppendLine("Sources:");
                    foreach (var s in task.Result.Sources) sb.AppendLine($"- {s}");
                }
                sb.AppendLine();
                sb.AppendLine("### Content");
                sb.AppendLine(task.Result.Content);
                sb.AppendLine();
            }

            if (task.Metadata.TryGetValue("QualityAssessment", out var qaObj) && qaObj is QualityAssessment qa)
            {
                sb.AppendLine("## Quality Assessment");
                sb.AppendLine($"- Needs More Research: {qa.NeedsMoreResearch}");
                sb.AppendLine($"- Reasoning: {qa.Reasoning}");
                if (qa.Gaps?.Any() == true)
                {
                    sb.AppendLine("Gaps:");
                    foreach (var g in qa.Gaps) sb.AppendLine($"- {g}");
                }
                sb.AppendLine();
            }

            // Include child summaries if any
            var children = _taskRegistry.Values.Where(t => t.ParentTaskId == taskId).ToList();
            if (children.Any())
            {
                sb.AppendLine("## Subtasks");
                foreach (var child in children)
                {
                    sb.AppendLine($"### {child.Description} ({child.Status})");
                    if (child.Result != null)
                    {
                        sb.AppendLine($"Confidence: {child.Result.ConfidenceScore:P1}");
                        sb.AppendLine($"Summary: {Truncate(child.Result.Content, 500)}");
                        sb.AppendLine();
                        sb.AppendLine("Raw Output:");
                        sb.AppendLine(Truncate(child.Result.Content, 4000));
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text;
            return text.Substring(0, max) + "...";
        }

        public ResearchTask? GetTaskStatus(Guid taskId)
        {
            return _taskRegistry.TryGetValue(taskId, out var task) ? task : null;
        }
    }

    // Usage example
    public class Program
    {
        public static async Task Main()
        {
            Console.WriteLine("🔬 Research Agent Network");
            Console.WriteLine("=========================");
            Console.WriteLine();

            // Setup configuration with proper priority order
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables() // Environment variables override config files
                .Build();

            try
            {
                // Create AI Provider using the factory pattern
                Console.WriteLine("🔧 Initializing AI Provider...");
                var aiProvider = AIProviderFactory.CreateProvider(configuration);
                Console.WriteLine($"✅ Using AI Provider: {aiProvider.GetProviderName()}");

                // Setup Kernel with AI service and embeddings
                Console.WriteLine("🔧 Configuring Semantic Kernel...");
                var builder = Kernel.CreateBuilder();
                aiProvider.ConfigureKernel(builder);
                aiProvider.ConfigureEmbeddings(builder);
                var kernel = builder.Build();
                Console.WriteLine("✅ Semantic Kernel configured successfully");

                // Toggle prompt logging from configuration
                var logPrompts = bool.TryParse(configuration["ResearchAgent:LogPrompts"], out var lp) && lp;
                KernelExtensions.EnablePromptLogging = logPrompts;

                // Get configuration values
                var maxConcurrency = int.Parse(configuration["ResearchAgent:MaxConcurrency"] ?? "5");
                var defaultPriority = int.Parse(configuration["ResearchAgent:DefaultPriority"] ?? "5");
                var maxDepth = int.Parse(configuration["ResearchAgent:MaxDecompositionDepth"] ?? "2");

                var orchestrator = new ResearchOrchestrator(kernel, maxConcurrency, maxDepth);

                Console.WriteLine($"🚀 Research Agent Network initialized with max concurrency: {maxConcurrency}, max depth: {maxDepth}, log prompts: {logPrompts}");
                Console.WriteLine();

                // Submit a complex research task
                var taskId = await orchestrator.SubmitResearchTask(
                    "Analyze the impact of quantum computing on cryptography, " +
                    "including current vulnerabilities, post-quantum algorithms, " +
                    "and migration strategies for enterprises",
                    defaultPriority
                );

                Console.WriteLine($"📋 Research task submitted with ID: {taskId}");
                Console.WriteLine("⏳ Monitoring progress...");
                Console.WriteLine();

                // Monitor progress
                while (true)
                {
                    var status = orchestrator.GetTaskStatus(taskId);
                    Console.WriteLine($"Task {taskId}: {status?.Status}");

                    if (status?.Status == TaskStatus.Completed)
                    {
                        Console.WriteLine();
                        Console.WriteLine("�� Research completed successfully!");
                        Console.WriteLine($"📊 Confidence Score: {status.Result?.ConfidenceScore:P1}");
                        Console.WriteLine($"📚 Sources: {status.Result?.Sources.Count ?? 0}");
                        Console.WriteLine();
                        Console.WriteLine("📄 Results:");
                        Console.WriteLine(status.Result?.Content);
                        break;
                    }
                    else if (status?.Status == TaskStatus.Failed)
                    {
                        Console.WriteLine();
                        Console.WriteLine("❌ Research failed!");
                        Console.WriteLine(status.Result?.Content);
                        break;
                    }

                    await Task.Delay(2000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("�� Configuration Help:");
                Console.WriteLine("To use this application, configure your AI provider settings:");
                Console.WriteLine();
                Console.WriteLine("1. In appsettings.json:");
                Console.WriteLine(@"{
                  ""AI_PROVIDER"": ""Ollama"",
                  ""Ollama"": {
                    ""ModelId"": ""llama3.1:latest"",
                    ""Endpoint"": ""http://localhost:11434"",
                    ""EmbeddingModelId"": ""llama3.1""
                  }
                }");
                Console.WriteLine();
                Console.WriteLine("2. Or via environment variables:");
                Console.WriteLine("   AI_PROVIDER=Ollama");
                Console.WriteLine("   Ollama__ModelId=llama3.1:latest");
                Console.WriteLine("   Ollama__Endpoint=http://localhost:11434");
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }


    }

    public static class JsonStuff
    {
        public static string GenerateJsonSchemaFromClass<T>()
        {
            JsonSerializerOptions options = JsonSerializerOptions.Default;
            JsonNode schema = options.GetJsonSchemaAsNode(typeof(T));
            return schema.ToString();
        }

    }
}
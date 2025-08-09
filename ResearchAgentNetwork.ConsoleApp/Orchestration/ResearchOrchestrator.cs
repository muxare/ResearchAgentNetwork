using System.Collections.Concurrent;
using System.Text;
using Microsoft.SemanticKernel;

namespace ResearchAgentNetwork;

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
        try { TaskEventPublished?.Invoke(e); } catch { }
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
            return _taskRegistry.Values.Where(t => t.ParentTaskId == parentId).ToList();
        });
        _agents["assessor"] = new QualityAssessmentAgent();
    }

    public async Task<Guid> SubmitResearchTask(string description, int priority = 5)
    {
        var task = new ResearchTask { Description = description, Priority = priority };

        _taskQueue.Enqueue(task);
        _taskRegistry[task.Id] = task;
        Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "submitted" });

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
                    try { await ProcessSingleTask(task); }
                    finally { _throttle.Release(); }
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

            var currentDepth = ComputeTaskDepth(task);
            if (currentDepth < _maxDecompositionDepth)
            {
                task.Status = TaskStatus.Analyzing;
                Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "status" });

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
                    task.Status = TaskStatus.Pending;
                    Console.WriteLine($"⏸️  Waiting for {subTasks.Count} subtasks to complete before aggregation");
                    Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "decomposed", Message = $"{subTasks.Count} subtasks" });
                    return;
                }

                var mergerResponse = await _agents["merger"].ProcessAsync(task, _kernel);
                if (mergerResponse.Data is ResearchTask mergedTask)
                {
                    task = mergedTask;
                }
            }
            else
            {
                Console.WriteLine($"🔚 Max decomposition depth {_maxDecompositionDepth} reached (current depth {currentDepth}). Executing directly.");
                task.Metadata["ForceExecute"] = true;
            }

            task.Status = TaskStatus.Executing;
            Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "status" });
            var executorResponse = await _agents["executor"].ProcessAsync(task, _kernel);
            if (executorResponse.Success && executorResponse.Data is ResearchResult result)
            {
                task.Result = result;
                task.Status = TaskStatus.Completed;
                Console.WriteLine($"✅ Completed task {task.Id} with confidence {task.Result.ConfidenceScore:P1}");
                Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "completed" });

                if (task.ParentTaskId.HasValue)
                {
                    await CheckParentAggregation(task.ParentTaskId.Value);
                }

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
                    task.Status = TaskStatus.Analyzing;
                    Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "status" });
                    _taskQueue.Enqueue(task);
                }
            }
        }
        catch (Exception ex)
        {
            task.Status = TaskStatus.Failed;
            task.Result = new ResearchResult { Content = $"Error: {ex.Message}", ConfidenceScore = 0 };
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
            var subTasks = _taskRegistry.Values.Where(t => t.ParentTaskId == parentId).ToList();

            if (subTasks.All(t => t.Status == TaskStatus.Completed))
            {
                parentTask.Status = TaskStatus.Aggregating;
                _taskQueue.Enqueue(parentTask);
                Publish(new TaskEvent { TaskId = parentTask.Id, Status = parentTask.Status, EventType = "status" });
            }
        }
    }

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
        return _taskRegistry.Values.GroupBy(t => t.Status).ToDictionary(g => g.Key, g => g.Count());
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
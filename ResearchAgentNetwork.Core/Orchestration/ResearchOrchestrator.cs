using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using ResearchAgentNetwork.SemanticMemory;

namespace ResearchAgentNetwork;

public class ResearchOrchestrator
{
    private readonly ConcurrentQueue<ResearchTask> _taskQueue = new();
    private readonly ConcurrentDictionary<Guid, ResearchTask> _taskRegistry = new();
    private readonly Dictionary<string, IResearchAgent> _agents = new();
    private readonly Kernel _kernel;
    private readonly ISemanticMemoryService? _memory;
    private readonly bool _enableWebSearch;
    private readonly SemaphoreSlim _throttle;
    private int _maxDecompositionDepth;
    private int _processorStarted = 0;
    private readonly int _retrievalTopK;
    private readonly int _maxRetryAttempts;
    private readonly double _pendingMergeThreshold;
    private readonly double _completedReuseThreshold;
    private readonly double _storeMinConfidence = 0.6;
    private readonly double _duplicateThreshold = 0.98;

    public ResearchOrchestrator(
        Kernel kernel,
        int maxConcurrency = 5,
        int maxDecompositionDepth = 2,
        ISemanticMemoryService? memory = null,
        int retrievalTopK = 3,
        int maxRetryAttempts = 1,
        double pendingMergeThreshold = 0.9,
        double completedReuseThreshold = 0.95,
        bool enableWebSearch = false,
        double storeMinConfidence = 0.6,
        double duplicateThreshold = 0.98)
    {
        _kernel = kernel;
        _memory = memory;
        _throttle = new SemaphoreSlim(maxConcurrency);
        _maxDecompositionDepth = Math.Max(0, maxDecompositionDepth);
        _retrievalTopK = Math.Max(1, retrievalTopK);
        _maxRetryAttempts = Math.Max(0, maxRetryAttempts);
        _pendingMergeThreshold = Math.Clamp(pendingMergeThreshold, 0.0, 1.0);
        _completedReuseThreshold = Math.Clamp(completedReuseThreshold, 0.0, 1.0);
        _enableWebSearch = enableWebSearch;
        _storeMinConfidence = Math.Clamp(storeMinConfidence, 0.0, 1.0);
        _duplicateThreshold = Math.Clamp(duplicateThreshold, 0.0, 1.0);
        InitializeAgents();
    }

    public event Action<TaskEvent>? TaskEventPublished;

    private void Publish(TaskEvent e)
    {
        try { TaskEventPublished?.Invoke(e); } catch { }
    }

    private static string JsonMsg(object details)
    {
        try
        {
            return "json:" + JsonSerializer.Serialize(details);
        }
        catch
        {
            return "json:{}";
        }
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
        _agents["retrieval_decision"] = new RetrievalDecisionAgent();
        _agents["query_planner"] = new QueryPlannerAgent();
        _agents["aggregator"] = new AggregatorAgent((Guid parentId) =>
        {
            return _taskRegistry.Values.Where(t => t.ParentTaskId == parentId).ToList();
        });
        _agents["assessor"] = new QualityAssessmentAgent();
        _agents["outline"] = new ReportOutlineAgent();
        _agents["section_writer"] = new SectionWriterAgent(_memory);
        _agents["fact_check"] = new FactCheckAgent(_memory);
        _agents["citation_manager"] = new CitationManagerAgent();
        _agents["curator"] = new KnowledgeCuratorAgent(_memory);
        _agents["memory_router"] = new MemoryRouterAgent();
        // Web search is injected later via setter when service is available
    }

    public void SetWebSearchAgent(WebSearchAgent agent)
    {
        _agents["websearch"] = agent;
    }

    public async Task<Guid> SubmitResearchTask(string description, int priority = 5)
    {
        var task = new ResearchTask { Description = description, Priority = priority };

        // De-dup/merge: if memory available, check similar pending tasks; if found, merge or drop
        if (_memory != null)
        {
            try
            {
                var similar = await _memory.RetrieveSimilarTasksAsync(description, topK: _retrievalTopK);
                // Find any pending tasks with sufficient similarity
                var pendingMatches = similar
                    .Where(s => s.Score >= _pendingMergeThreshold)
                    .Select(s => _taskRegistry.GetValueOrDefault(s.Id))
                    .Where(t => t != null && t.Status == TaskStatus.Pending)
                    .ToList();
                if (pendingMatches.Any())
                {
                    // Prefer the most similar pending match
                    var target = pendingMatches
                        .OrderByDescending(t => similar.First(s => s.Id == t!.Id).Score)
                        .First();
                    // Merge intent: append note to target; drop new task
                    target!.Description = target.Description + "\n(merged similar request) " + description;
                    Publish(new TaskEvent { TaskId = target.Id, Status = target.Status, EventType = "merged", Message = "Merged duplicate" });
                    return target.Id;
                }

                // If a completed task is very similar, avoid re-adding
                var completedMatches = similar
                    .Where(s => s.Score >= _completedReuseThreshold)
                    .Select(s => _taskRegistry.GetValueOrDefault(s.Id))
                    .Where(t => t != null && t.Status == TaskStatus.Completed)
                    .ToList();
                if (completedMatches.Any())
                {
                    // Prefer the most similar completed match
                    var target = completedMatches
                        .OrderByDescending(t => similar.First(s => s.Id == t!.Id).Score)
                        .First();
                    // Return the existing completed task id
                    return target!.Id;
                }
            }
            catch { }
        }

        _taskQueue.Enqueue(task);
        _taskRegistry[task.Id] = task;
        Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "submitted" });

        // Phase 0 hook: index task (optional)
        if (_memory != null)
        {
            _ = Task.Run(() => _memory.IndexTaskAsync(task));
        }

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

                    // Phase B: Outline planning stage after aggregation
                    try
                    {
                        var outlineResp = await _agents["outline"].ProcessAsync(task, _kernel);
                        if (outlineResp.Success && outlineResp.Data is ReportOutline outline)
                        {
                            Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "outlined", Message = $"sections:{outline.Sections.Count}" });

                            // Phase C: Write each section (best-effort, sequential for now)
                            foreach (var section in outline.Sections)
                            {
                                var req = new SectionWriterAgent.SectionWriteRequest
                                {
                                    SectionId = section.Id,
                                    Title = section.Title,
                                    Purpose = section.Purpose,
                                    EvidenceIds = section.EvidenceIds
                                };
                                task.Metadata["SectionWriteRequest"] = req;
                                try
                                {
                                    var writeResp = await _agents["section_writer"].ProcessAsync(task, _kernel);
                                    if (writeResp.Success && writeResp.Data is ReportSectionDraft draft)
                                    {
                                        Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "section_drafted", Message = draft.SectionId });

                                        // Phase D: Fact check the drafted section
                                        try
                                        {
                                            task.Metadata["FactCheckInput"] = new FactCheckAgent.FactCheckInput { SectionId = draft.SectionId, ContentMd = draft.ContentMd };
                                            var fcResp = await _agents["fact_check"].ProcessAsync(task, _kernel);
                                            if (fcResp.Success)
                                            {
                                                Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "section_fact_checked", Message = draft.SectionId });
                                            }
                                        }
                                        catch { }

                                        // Phase D: Normalize citations for the section
                                        try
                                        {
                                            task.Metadata["CitationNormalizeInput"] = new CitationManagerAgent.CitationNormalizeInput
                                            {
                                                SectionId = draft.SectionId,
                                                ContentMd = draft.ContentMd,
                                                RawCitations = draft.Citations,
                                                Style = "APA"
                                            };
                                            var cmResp = await _agents["citation_manager"].ProcessAsync(task, _kernel);
                                            if (cmResp.Success)
                                            {
                                                Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "section_citations_normalized", Message = draft.SectionId });
                                            }
                                        }
                                        catch { }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
                return;
            }

            var currentDepth = ComputeTaskDepth(task);
            if (currentDepth < _maxDecompositionDepth)
            {
                task.Status = TaskStatus.Analyzing;
                Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "status" });

                Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "agent_started", Message = JsonMsg(new { agent = "analyzer" }) });
                var analyzerResponse = await _agents["analyzer"].ProcessAsync(task, _kernel);
                if (analyzerResponse.Data is List<ResearchTask> subTasks)
                {
                    Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "agent_decision", Message = JsonMsg(new { agent = "analyzer", subtaskCount = subTasks.Count }) });
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
            // Retrieval decision + query planning + vector retrieval
            if (_memory != null && !task.Metadata.ContainsKey("ForceExecute"))
            {
                try
                {
                    // Decide if retrieval is needed
                    Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "agent_started", Message = JsonMsg(new { agent = "retrieval_decision" }) });
                    var decisionResp = await _agents["retrieval_decision"].ProcessAsync(task, _kernel);
                    bool requireRetrieval = true;
                    var types = new List<string> { "vector" };
                    if (decisionResp.Data is RetrievalDecisionAgent.RetrievalDecision dec)
                    {
                        requireRetrieval = dec.RequireRetrieval;
                        types = dec.RetrievalTypes.Count > 0 ? dec.RetrievalTypes : types;
                        Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "agent_decision", Message = JsonMsg(new { agent = "retrieval_decision", requireRetrieval = requireRetrieval, retrievalTypes = types }) });
                    }

                    if (requireRetrieval && types.Contains("vector"))
                    {
                        // Plan queries
                        Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "agent_started", Message = JsonMsg(new { agent = "query_planner" }) });
                        var planResp = await _agents["query_planner"].ProcessAsync(task, _kernel);
                        var queries = new List<string> { task.Description };
                        if (planResp.Data is QueryPlannerAgent.QueryPlan plan && plan.Queries.Count > 0)
                        {
                            queries = plan.Queries;
                            Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "agent_decision", Message = JsonMsg(new { agent = "query_planner", queries = queries }) });
                            // Save planned queries for downstream agents (e.g., web search)
                            task.Metadata["PlannedQueries"] = queries;
                        }
                        // Try vector retrieval using best query first
                        var retrieved = new List<RetrievedItem>();
                        foreach (var q in queries)
                        {
                            var ctx = await _memory.RetrieveSimilarResultsAsync(q, topK: _retrievalTopK);
                            if (ctx.Count > 0)
                            {
                                retrieved.AddRange(ctx.Select(c => new RetrievedItem(
                                    Kind: "memory",
                                    Snippet: c.Payload ?? string.Empty,
                                    Title: null,
                                    Url: null,
                                    Score: c.Score,
                                    ChunkIndex: c.Metadata != null && c.Metadata.TryGetValue("chunkIndex", out var ci) && ci is int cix ? cix : null,
                                    TotalChunks: c.Metadata != null && c.Metadata.TryGetValue("totalChunks", out var tc) && tc is int tcx ? tcx : null
                                )));
                            }
                            if (retrieved.Count >= _retrievalTopK) break;
                        }
                        if (retrieved.Count > 0)
                        {
                            task.Metadata["RetrievedContext"] = retrieved.Take(_retrievalTopK).ToList();
                            Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "retrieved", Message = $"memory:{retrieved.Count}" });
                        }
                    }
                }
                catch { }
            }

            // Optional web search enrichment under feature flag
            if (_enableWebSearch && _agents.TryGetValue("websearch", out var webAgent))
            {
                try
                {
                    var webResp = await webAgent.ProcessAsync(task, _kernel);
                    if (webResp.Success)
                    {
                        Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "ingested", Message = webResp.Message });
                    }
                }
                catch { }
            }

            Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "agent_started", Message = JsonMsg(new { agent = "executor" }) });
            var executorResponse = await _agents["executor"].ProcessAsync(task, _kernel);
            if (executorResponse.Success && executorResponse.Data is ResearchResult result)
            {
                task.Result = result;
                try
                {
                    int retrievedCount = 0;
                    if (task.Metadata.TryGetValue("RetrievedContext", out var rc) && rc is List<RetrievedItem> list)
                    {
                        retrievedCount = list.Count;
                    }
                    Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "agent_decision", Message = JsonMsg(new { agent = "executor", retrievedCount }) });
                }
                catch { }
                task.Status = TaskStatus.Completed;
                Console.WriteLine($"✅ Completed task {task.Id} with confidence {task.Result.ConfidenceScore:P1}");
                Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "completed" });

                // Store decision policy: index only if worth keeping
                if (_memory != null && task.Result != null)
                {
                    try
                    {
                        var shouldStore = await ShouldStoreResultAsync(task, task.Result);
                        if (shouldStore)
                        {
                            _ = Task.Run(() => _memory.IndexResultAsync(task, task.Result!));
                            Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "stored", Message = "Result stored in memory" });

                            // Phase E: Run memory routing and curation hooks
                            try
                            {
                                var routeResp = await _agents["memory_router"].ProcessAsync(task, _kernel);
                                if (routeResp.Success && routeResp.Data is MemoryRouterAgent.RouteDecision route)
                                {
                                    task.Metadata["MemoryRouteApplied"] = route;
                                    Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "routed" });
                                }
                            }
                            catch { }

                            try
                            {
                                task.Metadata["CurateInput"] = new KnowledgeCuratorAgent.CurateInput { Query = task.Description, TopK = _retrievalTopK, DuplicateThreshold = _duplicateThreshold };
                                var curResp = await _agents["curator"].ProcessAsync(task, _kernel);
                                if (curResp.Success)
                                {
                                    Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "curated" });
                                }
                            }
                            catch { }
                        }
                        else
                        {
                            Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "skipped", Message = "Result storage skipped" });
                        }
                    }
                    catch { }
                }

                if (task.ParentTaskId.HasValue)
                {
                    await CheckParentAggregation(task.ParentTaskId.Value);
                }

                // QA assessment and single refinement loop
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
                else if (task.Metadata.TryGetValue("QualityAssessment", out var qaObj) && qaObj is QualityAssessment qa && qa.NeedsMoreResearch)
                {
                    try
                    {
                        // Plan queries based on gaps
                        var planResp = await _agents["query_planner"].ProcessAsync(task, _kernel);
                        var queries = new List<string> { task.Description };
                        if (planResp.Data is QueryPlannerAgent.QueryPlan plan && plan.Queries.Count > 0)
                        {
                            queries = plan.Queries;
                        }
                        // Retrieve extra memory context
                        if (_memory != null)
                        {
                            var extra = new List<RetrievedItem>();
                            foreach (var q in queries)
                            {
                                var ctx = await _memory.RetrieveSimilarResultsAsync(q, topK: _retrievalTopK);
                                extra.AddRange(ctx.Select(c => new RetrievedItem(
                                    Kind: "memory",
                                    Snippet: c.Payload ?? string.Empty,
                                    Title: null,
                                    Url: null,
                                    Score: c.Score,
                                    ChunkIndex: c.Metadata != null && c.Metadata.TryGetValue("chunkIndex", out var ci) && ci is int cix ? cix : null,
                                    TotalChunks: c.Metadata != null && c.Metadata.TryGetValue("totalChunks", out var tc) && tc is int tcx ? tcx : null
                                )));
                                if (extra.Count >= _retrievalTopK) break;
                            }
                            if (extra.Count > 0)
                            {
                                task.Metadata["RetrievedContext"] = extra.Take(_retrievalTopK).ToList();
                            }
                        }
                        // Re-execute once with additional evidence
                        var refine = await _agents["executor"].ProcessAsync(task, _kernel);
                        if (refine.Success && refine.Data is ResearchResult improved)
                        {
                            task.Result = improved;
                            Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "refined" });
                        }
                    }
                    catch { }
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
                        if (task.ParentTaskId.HasValue)
                        {
                            await CheckParentAggregation(task.ParentTaskId.Value);
                        }
                    }
                    else
                    {
                        // Decide on retry or final failure
                        var totalAttempts = (int)task.Metadata["ExecAttempts"];
                        if (totalAttempts <= _maxRetryAttempts)
                        {
                            Console.WriteLine($"🔁 Retrying task {task.Id} (attempt {totalAttempts}/{_maxRetryAttempts})");
                            task.Status = TaskStatus.Pending;
                            task.Metadata.Remove("ForceExecute");
                            _taskQueue.Enqueue(task);
                            Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "retry" });
                        }
                        else
                        {
                            task.Status = TaskStatus.Failed;
                            Publish(new TaskEvent { TaskId = task.Id, Status = task.Status, EventType = "failed", Message = "Forced execution failed" });
                            if (task.ParentTaskId.HasValue)
                            {
                                await CheckParentAggregation(task.ParentTaskId.Value);
                            }
                        }
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

    private async Task<bool> ShouldStoreResultAsync(ResearchTask task, ResearchResult result)
    {
        // Policy: min length, min confidence, not near-duplicate
        const int minLength = 400;

        if (string.IsNullOrWhiteSpace(result.Content) || result.Content.Length < minLength)
        {
            return false;
        }
        if (result.ConfidenceScore < _storeMinConfidence)
        {
            return false;
        }
        if (_memory != null)
        {
            try
            {
                var similar = await _memory.RetrieveSimilarResultsAsync(result.Content, topK: 3);
                if (similar.Any(s => s.Score >= _duplicateThreshold))
                {
                    return false;
                }
            }
            catch { }
        }
        return true;
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

            bool IsPermanentlyFailed(ResearchTask t)
            {
                if (t.Status != TaskStatus.Failed) return false;
                if (t.Metadata.TryGetValue("ExecAttempts", out var att) && att is int a)
                {
                    return a > _maxRetryAttempts;
                }
                return false;
            }

            // Proceed to aggregation if all children are either completed or permanently failed
            if (subTasks.All(t => t.Status == TaskStatus.Completed || IsPermanentlyFailed(t)))
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
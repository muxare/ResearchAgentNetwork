using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ResearchAgentNetwork;
using ResearchAgentNetwork.AIProviders;
using KernelExtensionsApp = ResearchAgentNetwork.KernelExtensions;
using ResearchAgentNetwork.SemanticMemory;
using ResearchAgentNetwork.Infrastructure.SemanticMemory;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;
using Microsoft.EntityFrameworkCore;
using ResearchAgentNetwork.Persistence;
using ResearchAgentNetwork.Persistence.Entities;
using System.Text.Json.Serialization;
using ResearchAgentNetwork.WebSearch;
using ResearchAgentNetwork.Infrastructure.WebSearch;

var builder = WebApplication.CreateBuilder(args);
// Persistence
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "ran.db");
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite($"Data Source={dbPath}"));
Console.WriteLine($"📦 Sqlite DB path: {dbPath}");

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o => { o.SingleLine = false; o.TimestampFormat = "HH:mm:ss "; });

// Reuse configuration pattern
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Build Kernel via provider (defer Build until after VectorDb DI wiring)
var aiProvider = AIProviderFactory.CreateProvider(builder.Configuration);
var kernelBuilder = Kernel.CreateBuilder();
aiProvider.ConfigureKernel(kernelBuilder);
aiProvider.ConfigureEmbeddings(kernelBuilder);

// Settings
var maxConcurrency = int.Parse(builder.Configuration["ResearchAgent:MaxConcurrency"] ?? "5");
var maxDepth = int.Parse(builder.Configuration["ResearchAgent:MaxDecompositionDepth"] ?? "2");
var logPrompts = bool.TryParse(builder.Configuration["ResearchAgent:LogPrompts"], out var lp) && lp;
KernelExtensionsApp.EnablePromptLogging = logPrompts;
// Serialize enums as strings for frontend rendering
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Orchestrator singleton
// Optional semantic memory wiring (Phase 0 - in-memory)
ISemanticMemoryService? memory = null;
var vectorProvider = builder.Configuration["VectorDb:Provider"] ?? "None";
bool useQdrant = string.Equals(vectorProvider, "Qdrant", StringComparison.OrdinalIgnoreCase);
string endpoint = builder.Configuration["VectorDb:Endpoint"] ?? "localhost:6334";
var (qHost, qPort) = ParseQdrantEndpoint(endpoint);
if (useQdrant)
{
    Console.WriteLine($"Qdrant target: {qHost}:{qPort} (gRPC)");
    kernelBuilder.Services.AddSingleton(sp => new QdrantClient(qHost, qPort));
    kernelBuilder.Services.AddQdrantVectorStore();
}

var kernel = kernelBuilder.Build();

if (!string.Equals(vectorProvider, "None", StringComparison.OrdinalIgnoreCase))
{
    if (useQdrant)
    {
        try
        {
            var qc = kernel.Services.GetRequiredService<QdrantClient>();
            await qc.ListCollectionsAsync();
            Console.WriteLine($"✅ Qdrant reachable at: {qHost}:{qPort}");
        }
        catch (Exception qex)
        {
            Console.WriteLine($"⚠️ Qdrant not reachable at: {qHost}:{qPort}. Proceeding without vector memory. Error: {qex.Message}");
        }
        var configuredPrefix = builder.Configuration["VectorDb:CollectionPrefix"] ?? "ran";
        var collectionPrefix = string.IsNullOrWhiteSpace(configuredPrefix) ? $"ran_{768}" : $"{configuredPrefix}_{768}";
        var adapter = new SkVectorStoreAdapter(kernel, kernel.Services.GetRequiredService<QdrantClient>(), collectionPrefix, vectorDimensions: 768);
        var embeddingService = new EmbeddingService(kernel);
        memory = new SemanticMemoryService(adapter, embeddingService);
    }
    else
    {
        var vectorStore = new InMemoryVectorStore();
        var embeddingService = new EmbeddingService(kernel);
        memory = new SemanticMemoryService(vectorStore, embeddingService);
    }
}

var topK = int.Parse(builder.Configuration["VectorDb:TopK"] ?? "3");
var maxRetry = int.Parse(builder.Configuration["ResearchAgent:MaxRetries"] ?? "1");
var pendingMergeThreshold = double.TryParse(builder.Configuration["ResearchAgent:Merging:PendingThreshold"], out var pth) ? pth : 0.9;
var completedReuseThreshold = double.TryParse(builder.Configuration["ResearchAgent:Merging:CompletedThreshold"], out var cth) ? cth : 0.95;
var enableWebSearch = bool.TryParse(builder.Configuration["ResearchAgent:EnableWebSearch"], out var ews) && ews;
var storeMinConfidence = double.TryParse(builder.Configuration["ResearchAgent:Rag:StoreMinConfidence"], out var smc) ? smc : 0.6;
var duplicateThreshold = double.TryParse(builder.Configuration["ResearchAgent:Rag:DuplicateThreshold"], out var dth) ? dth : 0.98;
var orchestrator = new ResearchOrchestrator(
    kernel,
    maxConcurrency,
    maxDepth,
    memory,
    retrievalTopK: topK,
    maxRetryAttempts: maxRetry,
    pendingMergeThreshold: pendingMergeThreshold,
    completedReuseThreshold: completedReuseThreshold,
    enableWebSearch: enableWebSearch,
    storeMinConfidence: storeMinConfidence,
    duplicateThreshold: duplicateThreshold);
var appState = new AppState
{
    MaxConcurrency = maxConcurrency,
    MaxDecompositionDepth = maxDepth,
    LogPrompts = logPrompts
};

// Simple in-memory subscribers list for SSE
var subscribers = new List<HttpResponse>();
var sync = new object();
var writeSemaphores = new Dictionary<HttpResponse, SemaphoreSlim>();
SemaphoreSlim GetWriteSemaphore(HttpResponse r)
{
    lock (sync)
    {
        if (!writeSemaphores.TryGetValue(r, out var sem))
        {
            sem = new SemaphoreSlim(1, 1);
            writeSemaphores[r] = sem;
        }
        return sem;
    }
}
orchestrator.TaskEventPublished += (e) =>
{
    var (agent, details) = ExtractAgentAndDetails(e.Message);
    string payload = System.Text.Json.JsonSerializer.Serialize(new { type = "task", e.TaskId, e.Status, e.EventType, e.ParentTaskId, e.Message, e.TimestampUtc, Agent = agent, Details = details });
    List<HttpResponse> targets;
    lock (sync) { targets = subscribers.ToList(); }
    foreach (var resp in targets)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var sem = GetWriteSemaphore(resp);
                await sem.WaitAsync();
                try
                {
                    await resp.WriteAsync($"data: {payload}\n\n");
                    await resp.Body.FlushAsync();
                }
                finally
                {
                    sem.Release();
                }
            }
            catch { }
        });
    }
};

// Persist events and final reports automatically
orchestrator.TaskEventPublished += async (e) =>
{
    try
    {
        using var scope = builder.Services.BuildServiceProvider().CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Upsert task snapshot on event
        var t = orchestrator.GetTaskStatus(e.TaskId);
        if (t != null)
        {
            var te = await db.Tasks.FindAsync(t.Id);
            if (te == null)
            {
                db.Tasks.Add(new TaskEntity
                {
                    Id = t.Id,
                    Description = t.Description,
                    Priority = t.Priority,
                    Status = t.Status.ToString(),
                    CreatedAtUtc = t.CreatedAt,
                    UpdatedAtUtc = DateTime.UtcNow,
                    ParentTaskId = t.ParentTaskId
                });
            }
            else
            {
                te.Description = t.Description;
                te.Priority = t.Priority;
                te.Status = t.Status.ToString();
                te.UpdatedAtUtc = DateTime.UtcNow;
                te.ParentTaskId = t.ParentTaskId;
            }
        }
        var (agent, details) = ExtractAgentAndDetails(e.Message);
        db.TaskEvents.Add(new TaskEventEntity
        {
            TaskId = e.TaskId,
            EventType = e.EventType,
            Status = e.Status.ToString(),
            Message = e.Message,
            TimestampUtc = e.TimestampUtc,
            AgentRole = agent,
            DetailsJson = details
        });
        await db.SaveChangesAsync();

        if (e.EventType == "completed" || e.EventType == "failed")
        {
            var md = orchestrator.GenerateTaskReport(e.TaskId);
            var existing = await db.TaskReports.FindAsync(e.TaskId);
            if (existing == null)
            {
                db.TaskReports.Add(new TaskReportEntity { TaskId = e.TaskId, ReportMarkdown = md, GeneratedAtUtc = DateTime.UtcNow });
            }
            else
            {
                existing.ReportMarkdown = md;
                existing.GeneratedAtUtc = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
        }
    }
    catch { }
};

var app = builder.Build();
// EF: apply migrations on startup (simple EnsureCreated for now)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    try
    {
        EnsureTaskEventsColumns(db);
    }
    catch { }
}
// Initialize LLM logger now that app services are available
KernelExtensionsApp.Logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LLM");

// Configure Web Search provider and inject agent when enabled
if (enableWebSearch)
{
    // Default to NoOp unless explicitly configured via settings
    var searchProvider = builder.Configuration["WebSearch:Provider"] ?? "None";
    IWebSearchService webSearchService = searchProvider.ToLower() switch
    {
        "tavily" => new SKTavilyWebSearchService(builder.Configuration["WebSearch:Tavily:ApiKey"] ?? string.Empty),
        // Use direct Tavily API for richer provenance (title/url)
        "tavilyapi" => new TavilyWebSearchService(builder.Configuration["WebSearch:Tavily:ApiKey"] ?? string.Empty),
        _ => new NoOpWebSearchService()
    };
    var webSearchAgent = new WebSearchAgent(webSearchService, memory);
    orchestrator.SetWebSearchAgent(webSearchAgent);
}

app.UseDefaultFiles();
app.UseStaticFiles();

// DB info (debug)
app.MapGet("/api/dbinfo", () => new { path = dbPath, exists = System.IO.File.Exists(dbPath) });

// Submit task
app.MapPost("/api/tasks", async (TaskSubmit req, AppDbContext db) =>
{
    var id = await orchestrator.SubmitResearchTask(req.Description, req.Priority ?? 5);
    // Persist or update task row (basic fields)
    var t = orchestrator.GetTaskStatus(id);
    if (t != null)
    {
        var existing = await db.Tasks.FindAsync(id);
        if (existing == null)
        {
            db.Tasks.Add(new TaskEntity
            {
                Id = t.Id,
                Description = t.Description,
                Priority = t.Priority,
                Status = t.Status.ToString(),
                CreatedAtUtc = t.CreatedAt,
                UpdatedAtUtc = DateTime.UtcNow,
                ParentTaskId = t.ParentTaskId
            });
        }
        else
        {
            existing.Description = t.Description;
            existing.Priority = t.Priority;
            existing.Status = t.Status.ToString();
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.ParentTaskId = t.ParentTaskId;
        }
        await db.SaveChangesAsync();
    }
    return Results.Ok(new { id });
});

// Task by id
app.MapGet("/api/tasks/{id:guid}", (Guid id) =>
{
    var task = orchestrator.GetTaskStatus(id);
    return task is null ? Results.NotFound() : Results.Ok(task);
});

// Debug endpoint to refresh a single task status (forces re-fetch from orchestrator only)
app.MapPost("/api/tasks/{id:guid}/refresh", (Guid id) => Results.Ok(orchestrator.GetTaskStatus(id)));

// All tasks (trimmed)
app.MapGet("/api/tasks", () => orchestrator.GetAllTasks());

// Children
app.MapGet("/api/tasks/{id:guid}/children", (Guid id) => Results.Ok(orchestrator.GetChildren(id)));

// Progress summary
app.MapGet("/api/progress", () => orchestrator.GetProgressSummary());

// Settings read
app.MapGet("/api/settings", () => Results.Ok(appState));

// Report
app.MapGet("/api/tasks/{id:guid}/report", (Guid id) =>
{
    var report = orchestrator.GenerateTaskReport(id);
    return Results.Text(report, "text/plain");
});

// Events for a task (Phase 1 timeline API)
app.MapGet("/api/tasks/{id:guid}/events", async (Guid id, bool? includeChildren, int? top, int? skip, AppDbContext db) =>
{
    var include = includeChildren ?? false;
    var take = Math.Clamp(top ?? 500, 1, 5000);
    var sk = Math.Max(0, skip ?? 0);

    var ids = new HashSet<Guid> { id };
    if (include)
    {
        // BFS over Tasks table to collect all descendants
        var queue = new Queue<Guid>();
        queue.Enqueue(id);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            var children = await db.Tasks.Where(t => t.ParentTaskId == cur).Select(t => t.Id).ToListAsync();
            foreach (var cid in children)
            {
                if (ids.Add(cid)) queue.Enqueue(cid);
            }
        }
    }

    var events = await db.TaskEvents
        .Where(e => ids.Contains(e.TaskId))
        .OrderBy(e => e.TimestampUtc)
        .Skip(sk)
        .Take(take)
        .ToListAsync();

    return Results.Ok(events);
});

// Persist report
app.MapPost("/api/tasks/{id:guid}/report", async (Guid id, AppDbContext db) =>
{
    var md = orchestrator.GenerateTaskReport(id);
    var existing = await db.TaskReports.FindAsync(id);
    if (existing == null)
    {
        db.TaskReports.Add(new TaskReportEntity { TaskId = id, ReportMarkdown = md, GeneratedAtUtc = DateTime.UtcNow });
    }
    else
    {
        existing.ReportMarkdown = md;
        existing.GeneratedAtUtc = DateTime.UtcNow;
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { id, saved = true });
});

// Get persisted report (latest)
app.MapGet("/api/tasks/{id:guid}/report/persisted", async (Guid id, AppDbContext db) =>
{
    var r = await db.TaskReports.FindAsync(id);
    if (r == null) return Results.NotFound();
    return Results.Text(r.ReportMarkdown, "text/plain");
});

// Admin APIs
app.MapGet("/admin/tasks", async (AppDbContext db, string? status, string? q, int? top, int? skip) =>
{
    var query = db.Tasks.AsQueryable();
    if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
    if (!string.IsNullOrWhiteSpace(q))
    {
        var term = q.ToLower();
        query = query.Where(t => t.Description.ToLower().Contains(term) || t.Id.ToString().ToLower().Contains(term));
    }
    var take = Math.Clamp(top ?? 100, 1, 1000);
    var sk = Math.Max(0, skip ?? 0);
    var list = await query.OrderByDescending(t => t.CreatedAtUtc).Skip(sk).Take(take).ToListAsync();
    return Results.Ok(list);
});

app.MapGet("/admin/events", async (AppDbContext db, Guid? taskId, int? top, int? skip) =>
{
    var query = db.TaskEvents.AsQueryable();
    if (taskId.HasValue) query = query.Where(e => e.TaskId == taskId.Value);
    var take = Math.Clamp(top ?? 200, 1, 5000);
    var sk = Math.Max(0, skip ?? 0);
    var list = await query.OrderByDescending(e => e.TimestampUtc).Skip(sk).Take(take).ToListAsync();
    return Results.Ok(list);
});

app.MapGet("/admin/reports/{id:guid}.md", async (Guid id, AppDbContext db) =>
{
    var r = await db.TaskReports.FindAsync(id);
    if (r == null) return Results.NotFound();
    return Results.Text(r.ReportMarkdown, "text/markdown");
});

app.MapGet("/admin/reports/{id:guid}/download", async (Guid id, AppDbContext db) =>
{
    var r = await db.TaskReports.FindAsync(id);
    if (r == null) return Results.NotFound();
    var bytes = System.Text.Encoding.UTF8.GetBytes(r.ReportMarkdown);
    return Results.File(bytes, "text/markdown", fileDownloadName: $"report-{id}.md");
});

// Simple SSE feed (poll-based publish of current status every 1s)
app.MapGet("/api/events", async (HttpContext context) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");
    context.Response.Headers.Append("X-Accel-Buffering", "no");

    lock (sync) { subscribers.Add(context.Response); }

    // Send initial progress snapshot
    var summary = orchestrator.GetProgressSummary();
    var init = System.Text.Json.JsonSerializer.Serialize(new { type = "progress", summary });
    {
        var sem = GetWriteSemaphore(context.Response);
        await sem.WaitAsync(context.RequestAborted);
        try
        {
            await context.Response.WriteAsync($"data: {init}\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }
        finally
        {
            sem.Release();
        }
    }

    var ct = context.RequestAborted;
    try
    {
        // Heartbeat loop to keep the connection alive
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(20), ct);
            try
            {
                var sem = GetWriteSemaphore(context.Response);
                await sem.WaitAsync(ct);
                try
                {
                    await context.Response.WriteAsync($": ping {DateTime.UtcNow:o}\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);
                }
                finally
                {
                    sem.Release();
                }
            }
            catch { }
        }
    }
    catch (TaskCanceledException)
    {
        // client disconnected
    }
    finally
    {
        lock (sync)
        {
            subscribers.Remove(context.Response);
            if (writeSemaphores.TryGetValue(context.Response, out var sem))
            {
                writeSemaphores.Remove(context.Response);
                sem.Dispose();
            }
        }
    }
});

// Runtime settings update
app.MapPost("/api/settings", (SettingsDto req) =>
{
    if (req.MaxDecompositionDepth.HasValue)
    {
        orchestrator.UpdateMaxDecompositionDepth(req.MaxDecompositionDepth.Value);
        appState.MaxDecompositionDepth = req.MaxDecompositionDepth.Value;
    }
    if (req.LogPrompts.HasValue)
    {
        KernelExtensionsApp.EnablePromptLogging = req.LogPrompts.Value;
        appState.LogPrompts = req.LogPrompts.Value;
    }
    return Results.Ok(new
    {
        maxDecompositionDepth = appState.MaxDecompositionDepth,
        logPrompts = appState.LogPrompts
    });
});

// Task actions
app.MapMethods("/api/tasks/{id:guid}", new[] { "PATCH" }, (Guid id, TaskActionDto action) =>
{
    bool ok = action.Action?.ToLowerInvariant() switch
    {
        "cancel" => orchestrator.CancelTask(id),
        "retry" => orchestrator.RetryTask(id),
        "force" => orchestrator.ForceExecute(id),
        _ => false
    };
    return ok ? Results.NoContent() : Results.NotFound();
});

app.Run();

static (string host, int port) ParseQdrantEndpoint(string? endpoint)
{
    const int defaultGrpcPort = 6334;
    if (string.IsNullOrWhiteSpace(endpoint)) return ("localhost", defaultGrpcPort);

    if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
    {
        var host = string.IsNullOrWhiteSpace(uri.Host) ? "localhost" : uri.Host;
        var port = uri.Port > 0 ? uri.Port : defaultGrpcPort;
        if (port == 6333) port = defaultGrpcPort;
        return (host, port);
    }

    var raw = endpoint.Trim();
    var idx = raw.LastIndexOf(':');
    if (idx > 0 && idx < raw.Length - 1 && !raw.Contains("\\"))
    {
        var hostPart = raw.Substring(0, idx);
        var portPart = raw.Substring(idx + 1);
        if (int.TryParse(portPart, out var p))
        {
            if (p == 6333) p = defaultGrpcPort;
            return (string.IsNullOrWhiteSpace(hostPart) ? "localhost" : hostPart, p);
        }
    }

    return (raw, defaultGrpcPort);
}

static (string? agent, string? detailsJson) ExtractAgentAndDetails(string? message)
{
    if (string.IsNullOrWhiteSpace(message)) return (null, null);
    var s = message!;
    if (!s.StartsWith("json:")) return (null, null);
    var json = s.Substring(5);
    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        string? agent = null;
        if (doc.RootElement.TryGetProperty("agent", out var av) && av.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            agent = av.GetString();
        }
        return (agent, json);
    }
    catch
    {
        return (null, null);
    }
}

static void EnsureTaskEventsColumns(AppDbContext db)
{
    var conn = db.Database.GetDbConnection();
    conn.Open();
    try
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info('TaskEvents')";
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                cols.Add(reader.GetString(1)); // name column
            }
        }
        if (!cols.Contains("AgentRole"))
        {
            using var alter1 = conn.CreateCommand();
            alter1.CommandText = "ALTER TABLE TaskEvents ADD COLUMN AgentRole TEXT";
            alter1.ExecuteNonQuery();
        }
        if (!cols.Contains("DetailsJson"))
        {
            using var alter2 = conn.CreateCommand();
            alter2.CommandText = "ALTER TABLE TaskEvents ADD COLUMN DetailsJson TEXT";
            alter2.ExecuteNonQuery();
        }
    }
    finally
    {
        conn.Close();
    }
}

public record TaskSubmit(string Description, int? Priority);
public record SettingsDto(int? MaxDecompositionDepth, bool? LogPrompts);
public record TaskActionDto(string Action);
public class AppState
{
    public int MaxConcurrency { get; set; }
    public int MaxDecompositionDepth { get; set; }
    public bool LogPrompts { get; set; }
}

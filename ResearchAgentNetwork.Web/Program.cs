using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using ResearchAgentNetwork;
using ResearchAgentNetwork.AIProviders;
using KernelExtensionsApp = ResearchAgentNetwork.KernelExtensions;

var builder = WebApplication.CreateBuilder(args);

// Reuse configuration pattern
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Build Kernel via provider
var aiProvider = AIProviderFactory.CreateProvider(builder.Configuration);
var kernelBuilder = Kernel.CreateBuilder();
aiProvider.ConfigureKernel(kernelBuilder);
aiProvider.ConfigureEmbeddings(kernelBuilder);
var kernel = kernelBuilder.Build();

// Settings
var maxConcurrency = int.Parse(builder.Configuration["ResearchAgent:MaxConcurrency"] ?? "5");
var maxDepth = int.Parse(builder.Configuration["ResearchAgent:MaxDecompositionDepth"] ?? "2");
var logPrompts = bool.TryParse(builder.Configuration["ResearchAgent:LogPrompts"], out var lp) && lp;
KernelExtensionsApp.EnablePromptLogging = logPrompts;

// Orchestrator singleton
var orchestrator = new ResearchOrchestrator(kernel, maxConcurrency, maxDepth);
var appState = new AppState
{
    MaxConcurrency = maxConcurrency,
    MaxDecompositionDepth = maxDepth,
    LogPrompts = logPrompts
};

// Simple in-memory subscribers list for SSE
var subscribers = new List<HttpResponse>();
var sync = new object();
orchestrator.TaskEventPublished += (e) =>
{
    string payload = System.Text.Json.JsonSerializer.Serialize(new { type = "task", e.TaskId, e.Status, e.EventType, e.ParentTaskId, e.Message, e.TimestampUtc });
    List<HttpResponse> targets;
    lock (sync) { targets = subscribers.ToList(); }
    foreach (var resp in targets)
    {
        try
        {
            resp.WriteAsync($"data: {payload}\n\n").GetAwaiter().GetResult();
            resp.Body.Flush();
        }
        catch { }
    }
};

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Submit task
app.MapPost("/api/tasks", async (TaskSubmit req) =>
{
    var id = await orchestrator.SubmitResearchTask(req.Description, req.Priority ?? 5);
    return Results.Ok(new { id });
});

// Task by id
app.MapGet("/api/tasks/{id:guid}", (Guid id) =>
{
    var task = orchestrator.GetTaskStatus(id);
    return task is null ? Results.NotFound() : Results.Ok(task);
});

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

// Simple SSE feed (poll-based publish of current status every 1s)
app.MapGet("/api/events", async (HttpContext context) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    lock (sync) { subscribers.Add(context.Response); }
    // Send initial progress snapshot
    var summary = orchestrator.GetProgressSummary();
    var init = System.Text.Json.JsonSerializer.Serialize(new { type = "progress", summary });
    await context.Response.WriteAsync($"data: {init}\n\n");
    await context.Response.Body.FlushAsync();
    try
    {
        // Wait until client disconnects
        await Task.Delay(Timeout.Infinite, context.RequestAborted);
    }
    catch (TaskCanceledException)
    {
        // client disconnected
    }
    finally
    {
        lock (sync) { subscribers.Remove(context.Response); }
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

public record TaskSubmit(string Description, int? Priority);
public record SettingsDto(int? MaxDecompositionDepth, bool? LogPrompts);
public record TaskActionDto(string Action);
public class AppState
{
    public int MaxConcurrency { get; set; }
    public int MaxDecompositionDepth { get; set; }
    public bool LogPrompts { get; set; }
}

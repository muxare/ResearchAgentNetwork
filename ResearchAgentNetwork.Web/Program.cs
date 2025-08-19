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
using ResearchAgentNetwork.Persistence.Repositories;
using ResearchAgentNetwork.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
// Persistence (configurable: SqlServer or Sqlite)
string dbProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
string? sqlitePath = null;
string? sqlServerConnectionString = null;
if (string.Equals(dbProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
    sqlServerConnectionString = builder.Configuration["Database:ConnectionString"]
        ?? builder.Configuration.GetConnectionString("Default")
        ?? "Server=localhost;Database=ResearchAgentNetwork;Trusted_Connection=True;TrustServerCertificate=True;";
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(sqlServerConnectionString));
    Console.WriteLine("📦 Using SQL Server for persistence");
}
else
{
    sqlitePath = Path.Combine(builder.Environment.ContentRootPath, "ran.db");
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite($"Data Source={sqlitePath}"));
    Console.WriteLine($"📦 Sqlite DB path: {sqlitePath}");
}

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

// JWT/Auth configuration
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ran.local";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ran.clients";
var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev_insecure_key_change_me";
var accessTokenMinutes = int.TryParse(builder.Configuration["Jwt:AccessTokenMinutes"], out var atm) ? atm : 30;
var refreshTokenDays = int.TryParse(builder.Configuration["Jwt:RefreshTokenDays"], out var rtd) ? rtd : 14;

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

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
// Register repositories (must occur before Build)
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

var app = builder.Build();

// Persist events and final reports automatically (registered after DI container is built)
orchestrator.TaskEventPublished += async (e) =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var eventRepo = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var reportRepo = scope.ServiceProvider.GetRequiredService<IReportRepository>();
        var t = orchestrator.GetTaskStatus(e.TaskId);
        if (t != null)
        {
            await taskRepo.UpsertTaskSnapshotAsync(t);
        }
        var (agent, details) = ExtractAgentAndDetails(e.Message);
        await eventRepo.AddEventAsync(new TaskEventEntity
        {
            TaskId = e.TaskId,
            EventType = e.EventType,
            Status = e.Status.ToString(),
            Message = e.Message,
            TimestampUtc = e.TimestampUtc,
            AgentRole = agent,
            DetailsJson = details
        });

        if (e.EventType == "completed" || e.EventType == "failed")
        {
            var md = orchestrator.GenerateTaskReport(e.TaskId);
            await reportRepo.UpsertReportAsync(e.TaskId, md, DateTime.UtcNow);
        }
    }
    catch { }
};
// EF: apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
// Initialize LLM logger now that app services are available
KernelExtensionsApp.Logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LLM");

// Auth middleware
app.UseAuthentication();
app.UseAuthorization();

// Configure Web Search provider and inject agent when enabled
if (enableWebSearch)
{
    // Default to NoOp unless explicitly configured via settings
    var searchProvider = builder.Configuration["WebSearch:Provider"] ?? "None";
    IWebSearchService webSearchService = searchProvider.ToLower() switch
    {
        "tavily" => new SKTavilyWebSearchService(builder.Configuration["WebSearch:Tavily:ApiKey"] ?? string.Empty),
        // Use direct Tavily API for richer provenance (title/url)
        "tavilyapi" => new TavilyWebSearchService(builder.Configuration["WebSearch:Tavily:ApiKey"] ?? string.Empty)
            .WithIncludeDomains(ParseAllowlist(builder.Configuration["WebSearch:Allowlist"])) ,
        _ => new NoOpWebSearchService()
    };

    // Wrap with allowlist and rate limiter as configured
    var allowlist = ParseAllowlist(builder.Configuration["WebSearch:Allowlist"]);
    if (allowlist.Length > 0)
    {
        webSearchService = new AllowlistedWebSearchService(webSearchService, allowlist);
    }
    var rpm = int.TryParse(builder.Configuration["WebSearch:RateLimit:RPM"], out var x) ? x : 30;
    var minIntervalMs = int.TryParse(builder.Configuration["WebSearch:RateLimit:MinIntervalMs"], out var y) ? y : 500;
    if (rpm > 0 || minIntervalMs > 0)
    {
        webSearchService = new RateLimitedWebSearchService(webSearchService, Math.Max(1, rpm), Math.Max(0, minIntervalMs));
    }
    var webSearchAgent = new WebSearchAgent(webSearchService, memory);
    orchestrator.SetWebSearchAgent(webSearchAgent);
}

static string[] ParseAllowlist(string? csv)
{
    if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<string>();
    return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(s => s.ToLowerInvariant())
        .Distinct()
        .ToArray();
}

app.UseDefaultFiles();
app.UseStaticFiles();

// DB info (debug)
app.MapGet("/api/dbinfo", () => new
{
    provider = dbProvider,
    sqlite = sqlitePath is null ? null : new { path = sqlitePath, exists = System.IO.File.Exists(sqlitePath) },
    sqlserver = sqlServerConnectionString is null ? null : new { connectionString = sqlServerConnectionString }
});

// Submit task
app.MapPost("/api/tasks", async (TaskSubmit req, ITaskRepository tasksRepo) =>
{
    var id = await orchestrator.SubmitResearchTask(req.Description, req.Priority ?? 5);
    // Persist or update task row (basic fields)
    var t = orchestrator.GetTaskStatus(id);
    if (t != null)
    {
        await tasksRepo.UpsertTaskSnapshotAsync(t);
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

// All tasks (trimmed, from orchestrator snapshot)
app.MapGet("/api/tasks", () => orchestrator.GetAllTasks());

// Admin tasks via repository (filters, paging, time window)
app.MapGet("/admin/tasks", async (ITaskRepository repo, string? status, string? q, DateTime? fromUtc, DateTime? toUtc, int? top, int? skip) =>
{
    var take = Math.Clamp(top ?? 100, 1, 1000);
    var sk = Math.Max(0, skip ?? 0);
    var list = await repo.QueryTasksAsync(status, q, fromUtc, toUtc, sk, take);
    return Results.Ok(list);
});

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

// ===== Auth Endpoints =====

string CreateAccessToken(UserEntity user, IEnumerable<string> roles)
{
    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.UserName),
        new Claim(ClaimTypes.Email, user.Email)
    };
    claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

    var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
    var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
        issuer: jwtIssuer,
        audience: jwtAudience,
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(accessTokenMinutes),
        signingCredentials: creds);
    return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
}

static string HashPassword(string password)
{
    return BCrypt.Net.BCrypt.HashPassword(password);
}

static bool VerifyPassword(string password, string hash)
{
    return BCrypt.Net.BCrypt.Verify(password, hash);
}

app.MapPost("/api/auth/register", async (RegisterRequest req, IUserRepository usersRepo) =>
{
    if (string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "Username and password are required" });

    if (await usersRepo.ExistsByUserNameAsync(req.UserName.ToUpperInvariant()))
        return Results.Conflict(new { error = "Username already exists" });

    var user = new UserEntity
    {
        UserName = req.UserName,
        Email = req.Email ?? string.Empty,
        DisplayName = req.DisplayName,
        PasswordHash = HashPassword(req.Password),
        EmailConfirmed = false,
        IsActive = true,
        SecurityStamp = Guid.NewGuid().ToString("N")
    };

    await usersRepo.CreateAsync(user, roles: Array.Empty<string>());
    return Results.Ok(new { id = user.Id, user = new { user.UserName, user.Email, user.DisplayName } });
});

app.MapPost("/api/auth/login", async (LoginRequest req, IUserRepository usersRepo, AppDbContext db) =>
{
    var normalized = (req.UserName ?? string.Empty).ToUpperInvariant();
    var user = await usersRepo.GetByUserNameAsync(normalized);
    if (user is null || !VerifyPassword(req.Password ?? string.Empty, user.PasswordHash) || !user.IsActive)
        return Results.Unauthorized();

    var roles = user.Roles.Select(r => r.Role.Name).ToArray();
    var accessToken = CreateAccessToken(user, roles);
    var refresh = new RefreshTokenEntity
    {
        UserId = user.Id,
        Token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
        CreatedAtUtc = DateTime.UtcNow,
        ExpiresAtUtc = DateTime.UtcNow.AddDays(refreshTokenDays)
    };
    db.RefreshTokens.Add(refresh);
    await db.SaveChangesAsync();
    return Results.Ok(new { accessToken, refreshToken = refresh.Token, expiresInMinutes = accessTokenMinutes, roles });
});

app.MapPost("/api/auth/refresh", async (RefreshRequest req, AppDbContext db, IUserRepository usersRepo) =>
{
    var token = await db.RefreshTokens.Include(r => r.User).ThenInclude(u => u.Roles).ThenInclude(ur => ur.Role).FirstOrDefaultAsync(r => r.Token == req.RefreshToken);
    if (token is null || !token.IsActive)
        return Results.Unauthorized();
    var roles = token.User.Roles.Select(r => r.Role.Name);
    var accessToken = CreateAccessToken(token.User, roles);
    return Results.Ok(new { accessToken, expiresInMinutes = accessTokenMinutes });
});

app.MapPost("/api/auth/logout", async (RefreshRequest req, AppDbContext db) =>
{
    var token = await db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == req.RefreshToken);
    if (token is null) return Results.Ok();
    token.RevokedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok();
});

// Events for a task (Phase 1 timeline API)
app.MapGet("/api/tasks/{id:guid}/events", async (Guid id, bool? includeChildren, int? top, int? skip, ITaskRepository tasksRepo, IEventRepository eventsRepo) =>
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
            var children = await tasksRepo.GetChildrenIdsAsync(cur);
            foreach (var cid in children)
            {
                if (ids.Add(cid)) queue.Enqueue(cid);
            }
        }
    }

    var events = await eventsRepo.GetEventsAsync(ids, sk, take);

    return Results.Ok(events);
});

// Persist report
app.MapPost("/api/tasks/{id:guid}/report", async (Guid id, IReportRepository reportsRepo) =>
{
    var md = orchestrator.GenerateTaskReport(id);
    await reportsRepo.UpsertReportAsync(id, md, DateTime.UtcNow);
    return Results.Ok(new { id, saved = true });
});

// Get persisted report (latest)
app.MapGet("/api/tasks/{id:guid}/report/persisted", async (Guid id, IReportRepository reportsRepo) =>
{
    var r = await reportsRepo.GetAsync(id);
    if (r == null) return Results.NotFound();
    return Results.Text(r.ReportMarkdown, "text/plain");
});

// New consolidated report APIs
app.MapGet("/api/reports/{id:guid}", async (Guid id, IReportRepository reportsRepo) =>
{
    var r = await reportsRepo.GetAsync(id);
    if (r == null) return Results.NotFound();
    return Results.Ok(new { taskId = r.TaskId, markdown = r.ReportMarkdown, generatedAtUtc = r.GeneratedAtUtc });
});

app.MapGet("/api/reports/{id:guid}/download", async (Guid id, string? format, IReportRepository reportsRepo) =>
{
    var r = await reportsRepo.GetAsync(id);
    if (r == null) return Results.NotFound();
    var bytes = System.Text.Encoding.UTF8.GetBytes(r.ReportMarkdown);
    if (string.IsNullOrWhiteSpace(format) || string.Equals(format, "md", StringComparison.OrdinalIgnoreCase))
    {
        return Results.File(bytes, "text/markdown", fileDownloadName: $"report-{id}.md");
    }
    return Results.BadRequest(new { error = "Unsupported format. Use format=md." });
});

app.MapGet("/admin/events", async (IEventRepository repo, Guid? taskId, DateTime? fromUtc, DateTime? toUtc, int? top, int? skip) =>
{
    var take = Math.Clamp(top ?? 200, 1, 5000);
    var sk = Math.Max(0, skip ?? 0);
    var list = await repo.QueryEventsAsync(taskId, fromUtc, toUtc, sk, take);
    return Results.Ok(list);
});

app.MapGet("/admin/reports/{id:guid}.md", async (Guid id, IReportRepository reportsRepo) =>
{
    var r = await reportsRepo.GetAsync(id);
    if (r == null) return Results.NotFound();
    return Results.Text(r.ReportMarkdown, "text/markdown");
});

app.MapGet("/admin/reports/{id:guid}/download", async (Guid id, IReportRepository reportsRepo) =>
{
    var r = await reportsRepo.GetAsync(id);
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

// Removed legacy SQLite column shim; migrations handle schema changes

public record TaskSubmit(string Description, int? Priority);
public record SettingsDto(int? MaxDecompositionDepth, bool? LogPrompts);
public record TaskActionDto(string Action);
public class AppState
{
    public int MaxConcurrency { get; set; }
    public int MaxDecompositionDepth { get; set; }
    public bool LogPrompts { get; set; }
}

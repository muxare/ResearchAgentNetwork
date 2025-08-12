using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ResearchAgentNetwork.SemanticMemory;
using ResearchAgentNetwork.AIProviders;
using Microsoft.Extensions.DependencyInjection;
using ResearchAgentNetwork.Infrastructure.SemanticMemory;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;
using System.Text;
using ResearchAgentNetwork.WebSearch;
using ResearchAgentNetwork.Infrastructure.WebSearch;

namespace ResearchAgentNetwork
{
    public class Program
    {
        private static bool PauseOnTaskEvents = false;
        private static bool IsAwaitingSpace = false;
        private sealed class TeeTextWriter : TextWriter
        {
            private readonly TextWriter _a;
            private readonly TextWriter _b;
            public TeeTextWriter(TextWriter a, TextWriter b) { _a = a; _b = b; }
            public override Encoding Encoding => _a.Encoding;
            public override void Write(char value) { _a.Write(value); _b.Write(value); }
            public override void Write(string? value) { _a.Write(value); _b.Write(value); }
            public override void WriteLine(string? value) { _a.WriteLine(value); _b.WriteLine(value); }
            public override Task WriteAsync(char value) { var t1 = _a.WriteAsync(value); var t2 = _b.WriteAsync(value); return Task.WhenAll(t1, t2); }
            public override Task WriteAsync(string? value) { var t1 = _a.WriteAsync(value); var t2 = _b.WriteAsync(value); return Task.WhenAll(t1, t2); }
            public override Task WriteLineAsync(string? value) { var t1 = _a.WriteLineAsync(value); var t2 = _b.WriteLineAsync(value); return Task.WhenAll(t1, t2); }
            protected override void Dispose(bool disposing) { if (disposing) { _a.Flush(); _b.Flush(); } base.Dispose(disposing); }
        }
        public static async Task Main()
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole(o => { o.SingleLine = false; o.TimestampFormat = "HH:mm:ss "; });
                builder.SetMinimumLevel(LogLevel.Information);
            });
            var logger = loggerFactory.CreateLogger("App");

            Console.WriteLine("🔬 Research Agent Network");
            Console.WriteLine("=========================");
            Console.WriteLine();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Prepare session logging directory (configurable)
            var configuredLogsPath = configuration["Logging:SessionLogsPath"];
            var sessionRoot = string.IsNullOrWhiteSpace(configuredLogsPath)
                ? Path.Combine(Directory.GetCurrentDirectory(), "SessionLogs")
                : configuredLogsPath;
            Directory.CreateDirectory(sessionRoot);
            var sessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var sessionDir = Path.Combine(sessionRoot, sessionId);
            Directory.CreateDirectory(sessionDir);

            using var consoleLogWriter = new StreamWriter(Path.Combine(sessionDir, "console.log")) { AutoFlush = true };
            var originalOut = Console.Out;
            Console.SetOut(new TeeTextWriter(originalOut, consoleLogWriter));
            using var consoleErrWriter = new StreamWriter(Path.Combine(sessionDir, "stderr.log")) { AutoFlush = true };
            var originalErr = Console.Error;
            Console.SetError(new TeeTextWriter(originalErr, consoleErrWriter));

            // Dump basic configuration to a file for later analysis
            File.WriteAllText(Path.Combine(sessionDir, "configuration.txt"), string.Join(Environment.NewLine, new[]
            {
                "CWD: " + Directory.GetCurrentDirectory(),
                "BaseDir: " + AppContext.BaseDirectory,
                "appsettings.json present at BaseDir: " + File.Exists(Path.Combine(AppContext.BaseDirectory, "appsettings.json")),
                "VectorDb:Provider: " + configuration["VectorDb:Provider"],
                "VectorDb:Endpoint: " + configuration["VectorDb:Endpoint"],
                "VectorDb:CollectionPrefix: " + configuration["VectorDb:CollectionPrefix"],
                "VectorDb:TopK: " + configuration.GetValue<int?>("VectorDb:TopK"),
                "ResearchAgent:MaxConcurrency: " + configuration.GetValue<int?>("ResearchAgent:MaxConcurrency"),
                "ResearchAgent:DefaultPriority: " + configuration.GetValue<int?>("ResearchAgent:DefaultPriority"),
                "ResearchAgent:MaxDecompositionDepth: " + configuration.GetValue<int?>("ResearchAgent:MaxDecompositionDepth"),
                "ResearchAgent:LogPrompts: " + configuration.GetValue<bool?>("ResearchAgent:LogPrompts")
            }));

            Console.WriteLine("CWD: " + Directory.GetCurrentDirectory());
            Console.WriteLine("BaseDir: " + AppContext.BaseDirectory);
            Console.WriteLine("appsettings.json present at BaseDir: " + File.Exists(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
            Console.WriteLine("VectorDb:Provider: " + configuration["VectorDb:Provider"]);
            Console.WriteLine("VectorDb:Endpoint: " + configuration["VectorDb:Endpoint"]);
            Console.WriteLine("VectorDb:CollectionPrefix: " + configuration["VectorDb:CollectionPrefix"]);
            Console.WriteLine("VectorDb:TopK: " + configuration.GetValue<int?>("VectorDb:TopK"));
            Console.WriteLine("ResearchAgent:MaxConcurrency: " + configuration.GetValue<int?>("ResearchAgent:MaxConcurrency"));
            Console.WriteLine("ResearchAgent:DefaultPriority: " + configuration.GetValue<int?>("ResearchAgent:DefaultPriority"));
            Console.WriteLine("ResearchAgent:MaxDecompositionDepth: " + configuration.GetValue<int?>("ResearchAgent:MaxDecompositionDepth"));
            Console.WriteLine("ResearchAgent:LogPrompts: " + configuration.GetValue<bool?>("ResearchAgent:LogPrompts"));

            try
            {
                Console.WriteLine("🔧 Initializing AI Provider...");
                var aiProvider = AIProviderFactory.CreateProvider(configuration);
                Console.WriteLine($"✅ Using AI Provider: {aiProvider.GetProviderName()}");

                Console.WriteLine("🔧 Configuring Semantic Kernel...");
                var builder = Kernel.CreateBuilder();
                aiProvider.ConfigureKernel(builder);
                aiProvider.ConfigureEmbeddings(builder);
                var kernel = builder.Build();
                Console.WriteLine("✅ Semantic Kernel configured successfully");

                var logPrompts = bool.TryParse(configuration["ResearchAgent:LogPrompts"], out var lp) && lp;
                KernelExtensions.EnablePromptLogging = logPrompts;
                KernelExtensions.Logger = loggerFactory.CreateLogger("LLM");

                var maxConcurrency = int.Parse(configuration["ResearchAgent:MaxConcurrency"] ?? "5");
                var defaultPriority = int.Parse(configuration["ResearchAgent:DefaultPriority"] ?? "5");
                var maxDepth = int.Parse(configuration["ResearchAgent:MaxDecompositionDepth"] ?? "2");
                var topK = int.Parse(configuration["VectorDb:TopK"] ?? "3");
                PauseOnTaskEvents = bool.TryParse(configuration["ResearchAgent:PauseOnEvents"], out var pe) && pe;

                // Optional semantic memory wiring (Phase 0 - in-memory)
                ISemanticMemoryService? memory = null;
                var vectorProvider = configuration["VectorDb:Provider"] ?? "None";
                Console.WriteLine("✅ VectorDb:Provider: " + vectorProvider);
                if (!string.Equals(vectorProvider, "None", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(vectorProvider, "Qdrant", StringComparison.OrdinalIgnoreCase))
                    {
                        var endpoint = configuration["VectorDb:Endpoint"] ?? "localhost:6334";
                        var (qHost, qPort) = ParseQdrantEndpoint(endpoint);
                        Console.WriteLine($"Qdrant target: {qHost}:{qPort} (gRPC)");
                        builder.Services.AddSingleton(sp => new QdrantClient(qHost, qPort));
                        builder.Services.AddQdrantVectorStore();
                        var serviceProvider = builder.Services.BuildServiceProvider();
                        // Connectivity check
                        try
                        {
                            var qc = serviceProvider.GetRequiredService<QdrantClient>();
                            await qc.ListCollectionsAsync();
                            Console.WriteLine($"✅ Qdrant reachable at: {qHost}:{qPort}");
                        }
                        catch (Exception qex)
                        {
                            Console.WriteLine($"⚠️ Qdrant not reachable at: {qHost}:{qPort}. Proceeding without vector memory. Error: {qex.Message}");
                            // Leave memory = null to disable vector features
                        }
                        var configuredPrefix = configuration["VectorDb:CollectionPrefix"] ?? "ran";
                        var collectionPrefix = string.IsNullOrWhiteSpace(configuredPrefix) ? $"ran_{768}" : $"{configuredPrefix}_{768}";
                        var adapter = new SkVectorStoreAdapter(kernel, serviceProvider.GetRequiredService<QdrantClient>(), collectionPrefix, vectorDimensions: 768);
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

                var maxRetry = int.Parse(configuration["ResearchAgent:MaxRetries"] ?? "1");
                var enableWebSearch = bool.TryParse(configuration["ResearchAgent:EnableWebSearch"], out var ews) && ews;
                var orchestrator = new ResearchOrchestrator(kernel, maxConcurrency, maxDepth, memory, retrievalTopK: topK, maxRetryAttempts: maxRetry, enableWebSearch: enableWebSearch);

                if (enableWebSearch)
                {
                    var searchProvider = configuration["WebSearch:Provider"] ?? "None";
                    IWebSearchService webSearchService = searchProvider.ToLower() switch
                    {
                        "tavily" => new SKTavilyWebSearchService(configuration["WebSearch:Tavily:ApiKey"] ?? string.Empty),
                        _ => new NoOpWebSearchService()
                    };
                    orchestrator.SetWebSearchAgent(new WebSearchAgent(webSearchService, memory));
                }

                // Subscribe to task events: write console snapshot and also persist to events.ndjson
                using var eventsWriter = new StreamWriter(Path.Combine(sessionDir, "events.ndjson")) { AutoFlush = true };
                var eventsLock = new object();
                orchestrator.TaskEventPublished += e =>
                {
                    try
                    {
                        PrintTasksSnapshot(orchestrator, e, PauseOnTaskEvents);
                        var json = System.Text.Json.JsonSerializer.Serialize(new { type = "task", e.TaskId, e.Status, e.EventType, e.ParentTaskId, e.Message, e.TimestampUtc });
                        lock (eventsLock)
                        {
                            eventsWriter.WriteLine(json);
                        }
                    }
                    catch
                    {
                        // Intentionally swallow to avoid crashing background processing
                    }
                };

                Console.WriteLine($"🚀 Research Agent Network initialized with max concurrency: {maxConcurrency}, max depth: {maxDepth}, log prompts: {logPrompts}");
                Console.WriteLine();

                var taskId = await orchestrator.SubmitResearchTask(
                    "Analyze the impact of quantum computing on cryptography, including current vulnerabilities, post-quantum algorithms, and migration strategies for enterprises",
                    defaultPriority
                );

                Console.WriteLine($"📋 Research task submitted with ID: {taskId}");
                Console.WriteLine("⏳ Monitoring progress...");
                Console.WriteLine();

                while (true)
                {
                    var status = orchestrator.GetTaskStatus(taskId);
                    if (!IsAwaitingSpace)
                    {
                        Console.WriteLine($"Task {taskId}: {status?.Status}");
                    }

                    if (status?.Status == TaskStatus.Completed)
                    {
                        Console.WriteLine();
                        Console.WriteLine("✅ Research completed successfully!");
                        Console.WriteLine($"📊 Confidence Score: {status.Result?.ConfidenceScore:P1}");
                        Console.WriteLine($"📚 Sources: {status.Result?.Sources.Count ?? 0}");
                        Console.WriteLine();
                        Console.WriteLine("📄 Results:");
                        Console.WriteLine(status.Result?.Content);
                        // Persist a final report for offline analysis
                        var reportPath = Path.Combine(sessionDir, $"report-{taskId}.md");
                        File.WriteAllText(reportPath, orchestrator.GenerateTaskReport(taskId));
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
                Console.WriteLine("Configuration Help:");
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

        private static (string host, int port) ParseQdrantEndpoint(string? endpoint)
        {
            // Accepts: "localhost", "localhost:6334", "http://localhost:6333", "http://localhost:6334"
            // Qdrant.Client uses gRPC; default to 6334
            const int defaultGrpcPort = 6334;
            if (string.IsNullOrWhiteSpace(endpoint)) return ("localhost", defaultGrpcPort);

            // If it's a valid absolute URI, extract host and prefer gRPC port
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            {
                var host = string.IsNullOrWhiteSpace(uri.Host) ? "localhost" : uri.Host;
                // If a port is provided but it's 6333 (HTTP), switch to 6334 for gRPC
                var port = uri.Port > 0 ? uri.Port : defaultGrpcPort;
                if (port == 6333) port = defaultGrpcPort;
                return (host, port);
            }

            // If it contains a colon without scheme, treat as host:port
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

            // Fallback: raw is host only; use default gRPC port
            return (raw, defaultGrpcPort);
        }

        private static void PrintTasksSnapshot(ResearchOrchestrator orchestrator, TaskEvent e, bool pause)
        {
            Console.WriteLine();
            Console.WriteLine($"📋 Task List Update ({e.EventType}): {e.TaskId} → {e.Status}");
            Console.WriteLine(new string('=', 60));

            var tasks = orchestrator.GetAllTasks()
                .OrderBy(t => t.CreatedAt)
                .ToList();

            if (tasks.Count == 0)
            {
                Console.WriteLine("No tasks.");
            }
            else
            {
                foreach (var t in tasks)
                {
                    var parent = t.ParentTaskId.HasValue ? $" (parent: {t.ParentTaskId.Value})" : string.Empty;
                    var subtasks = t.SubTaskIds.Any() ? $" (subtasks: {t.SubTaskIds.Count})" : string.Empty;
                    Console.WriteLine($"• {t.Id} - {t.Description}");
                    Console.WriteLine($"  Status: {t.Status} | Priority: {t.Priority}{parent}{subtasks}");
                    if (t.Result != null)
                    {
                        Console.WriteLine($"  Result: confidence {t.Result.ConfidenceScore:P1} | sources {t.Result.Sources.Count}");
                    }
                }
            }

            Console.WriteLine();
            if (pause)
            {
                Console.WriteLine("Press SPACE to continue...");
                IsAwaitingSpace = true;
                WaitForSpaceKey();
                IsAwaitingSpace = false;
            }
        }

        private static void WaitForSpaceKey()
        {
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Spacebar) return;
            }
        }
    }
}
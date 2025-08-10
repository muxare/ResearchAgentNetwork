using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ResearchAgentNetwork.SemanticMemory;
using ResearchAgentNetwork.AIProviders;
using Microsoft.Extensions.DependencyInjection;
using ResearchAgentNetwork.Infrastructure.SemanticMemory;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;

namespace ResearchAgentNetwork
{
    public class Program
    {
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
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

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

                // Optional semantic memory wiring (Phase 0 - in-memory)
                ISemanticMemoryService? memory = null;
                var vectorProvider = configuration["VectorDb:Provider"] ?? "None";
                if (!string.Equals(vectorProvider, "None", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(vectorProvider, "Qdrant", StringComparison.OrdinalIgnoreCase))
                    {
                        var endpoint = configuration["VectorDb:Endpoint"] ?? "http://localhost:6333";
                        builder.Services.AddSingleton(sp => new QdrantClient(endpoint));
                        builder.Services.AddQdrantVectorStore();
                        var serviceProvider = builder.Services.BuildServiceProvider();
                        var adapter = new QdrantVectorStoreAdapter(kernel, serviceProvider.GetRequiredService<QdrantClient>(), configuration["VectorDb:CollectionPrefix"] ?? "");
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

                var orchestrator = new ResearchOrchestrator(kernel, maxConcurrency, maxDepth, memory);

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
                    Console.WriteLine($"Task {taskId}: {status?.Status}");

                    if (status?.Status == TaskStatus.Completed)
                    {
                        Console.WriteLine();
                        Console.WriteLine("✅ Research completed successfully!");
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
    }
}
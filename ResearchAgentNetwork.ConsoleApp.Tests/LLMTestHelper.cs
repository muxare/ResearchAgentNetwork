using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using ResearchAgentNetwork.AIProviders;

namespace ResearchAgentNetwork.ConsoleApp.Tests
{
    public static class LLMTestHelper
    {
        public static async Task<bool> IsOllamaAvailableAsync()
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                var response = await httpClient.GetAsync("http://localhost:11434/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static Kernel CreateTestKernel()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.test.json", optional: false)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var ollamaConfig = configuration.GetSection("Ollama");
            var ollamaProvider = new OllamaProvider(
                ollamaConfig["ModelId"] ?? "llama3.1:latest",
                ollamaConfig["Endpoint"] ?? "http://localhost:11434",
                ollamaConfig["EmbeddingModelId"] ?? "llama3.1"
            );

            var validation = ollamaProvider.ValidateConfiguration();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Ollama configuration is invalid: {string.Join(", ", validation.Errors)}");
            }

            var builder = Kernel.CreateBuilder();
            ollamaProvider.ConfigureKernel(builder);
            ollamaProvider.ConfigureEmbeddings(builder);
            return builder.Build();
        }

        public static async Task<AgentResponse> ExecuteWithRetryAsync(Func<Task<AgentResponse>> action, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return await action();
                }
                catch (Exception) when (i < maxRetries - 1)
                {
                    // Wait before retry, with exponential backoff
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
                }
            }
            
            // Last attempt
            return await action();
        }

        public static bool IsSimpleTask(AgentResponse response)
        {
            return response.Data?.ToString()?.Contains("ReadyForExecution") == true;
        }

        public static bool IsComplexTask(AgentResponse response)
        {
            return response.Data is List<ResearchTask>;
        }

        public static List<ResearchTask> GetSubtasks(AgentResponse response)
        {
            return response.Data as List<ResearchTask> ?? new List<ResearchTask>();
        }
    }
} 
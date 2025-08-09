using Microsoft.Extensions.Configuration;

namespace ResearchAgentNetwork.AIProviders
{
    public static class AIProviderFactory
    {
        public static IAIProvider CreateProvider(IConfiguration configuration)
        {
            string provider = configuration["AI_PROVIDER"] ?? "Ollama";
            
            IAIProvider aiProvider = provider.ToLower() switch
            {
                "ollama" => new OllamaProvider(
                    configuration["Ollama:ModelId"] ?? "llama3.1:latest",
                    configuration["Ollama:Endpoint"] ?? "http://localhost:11434",
                    configuration["Ollama:EmbeddingModelId"] ?? "llama3.1"
                ),
                "azureopenai" => new AzureOpenAIProvider(
                    configuration["AzureOpenAI:DeploymentName"] ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName is required"),
                    configuration["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is required"),
                    configuration["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is required"),
                    configuration["AzureOpenAI:EmbeddingDeploymentName"] ?? "text-embedding-ada-002"
                ),
                "openai" => new OpenAIProvider(
                    configuration["OpenAI:ModelId"] ?? "gpt-4o-mini",
                    configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey is required"),
                    configuration["OpenAI:OrganizationId"],
                    configuration["OpenAI:EmbeddingModelId"] ?? "text-embedding-3-small"
                ),
                _ => throw new InvalidOperationException($"Unknown AI provider: {provider}")
            };

            // Validate configuration
            var validation = aiProvider.ValidateConfiguration();
            if (!validation.IsValid)
            {
                var errors = string.Join(", ", validation.Errors);
                throw new InvalidOperationException($"AI Provider configuration validation failed: {errors}");
            }

            if (validation.Warnings.Any())
            {
                var warnings = string.Join(", ", validation.Warnings);
                Console.WriteLine($"⚠️ AI Provider warnings: {warnings}");
            }

            return aiProvider;
        }
    }
}
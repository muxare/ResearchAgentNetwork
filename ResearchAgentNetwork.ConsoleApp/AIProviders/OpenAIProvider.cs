using Microsoft.SemanticKernel;

namespace ResearchAgentNetwork.AIProviders
{
    public class OpenAIProvider : IAIProvider
    {
        private readonly string _modelId;
        private readonly string _apiKey;
        private readonly string? _organizationId;
        private readonly string _embeddingModelId;

        public OpenAIProvider(string modelId, string apiKey, string? organizationId = null, string embeddingModelId = "text-embedding-3-small")
        {
            _modelId = modelId;
            _apiKey = apiKey;
            _organizationId = organizationId;
            _embeddingModelId = embeddingModelId;
        }

        public void ConfigureKernel(IKernelBuilder builder)
        {
            Console.WriteLine($"�� Configuring OpenAI chat completion with model: {_modelId}");
            builder.AddOpenAIChatCompletion(_modelId, _apiKey);
        }

        public void ConfigureEmbeddings(IKernelBuilder builder)
        {
            Console.WriteLine($"�� Configuring OpenAI embeddings with model: {_embeddingModelId}");
            builder.AddOpenAIEmbeddingGenerator(_embeddingModelId, _apiKey);
        }

        public string GetProviderName() => "OpenAI";

        public ValidationResult ValidateConfiguration()
        {
            var result = new ValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(_modelId))
                result.Errors.Add("ModelId is required");

            if (string.IsNullOrWhiteSpace(_apiKey))
                result.Errors.Add("ApiKey is required");

            if (_apiKey.Length < 20)
                result.Warnings.Add("ApiKey seems too short, please verify");

            if (string.IsNullOrWhiteSpace(_embeddingModelId))
                result.Errors.Add("EmbeddingModelId is required");

            result.IsValid = !result.Errors.Any();
            return result;
        }
    }
}
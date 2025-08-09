using Microsoft.SemanticKernel;

namespace ResearchAgentNetwork.AIProviders
{
    public class OllamaProvider : IAIProvider
    {
        private readonly string _modelId;
        private readonly string _endpoint;
        private readonly string _embeddingModelId;

        public OllamaProvider(string modelId, string endpoint, string embeddingModelId)
        {
            _modelId = modelId;
            _endpoint = endpoint;
            _embeddingModelId = embeddingModelId;
        }

        public void ConfigureKernel(IKernelBuilder builder)
        {
            Console.WriteLine($"�� Configuring Ollama chat completion with model: {_modelId}");
            builder.AddOllamaChatCompletion(_modelId, new Uri(_endpoint));
        }

        [Obsolete]
        public void ConfigureEmbeddings(IKernelBuilder builder)
        {
            Console.WriteLine($"�� Configuring Ollama embeddings with model: {_embeddingModelId}");
            try
            {
                builder.AddOllamaTextEmbeddingGeneration(
                    endpoint: new Uri(_endpoint),
                    modelId: _embeddingModelId);
                Console.WriteLine($"✅ Ollama embedding generator configured successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to configure Ollama embeddings: {ex.Message}");
                throw;
            }
        }

        public string GetProviderName() => "Ollama";

        public ValidationResult ValidateConfiguration()
        {
            var result = new ValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(_modelId))
                result.Errors.Add("ModelId is required");

            if (string.IsNullOrWhiteSpace(_endpoint))
                result.Errors.Add("Endpoint is required");

            if (!Uri.TryCreate(_endpoint, UriKind.Absolute, out _))
                result.Errors.Add("Endpoint must be a valid URI");

            if (string.IsNullOrWhiteSpace(_embeddingModelId))
                result.Warnings.Add("EmbeddingModelId not specified, using default");

            result.IsValid = !result.Errors.Any();
            return result;
        }
    }
}
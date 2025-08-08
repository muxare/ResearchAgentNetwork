using Microsoft.SemanticKernel;

namespace ResearchAgentNetwork.AIProviders
{
    public class AzureOpenAIProvider : IAIProvider
    {
        private readonly string _deploymentName;
        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly string _embeddingDeploymentName;

        public AzureOpenAIProvider(string deploymentName, string endpoint, string apiKey, string embeddingDeploymentName)
        {
            _deploymentName = deploymentName;
            _endpoint = endpoint;
            _apiKey = apiKey;
            _embeddingDeploymentName = embeddingDeploymentName;
        }

        public void ConfigureKernel(IKernelBuilder builder)
        {
            Console.WriteLine($"🔧 Configuring Azure OpenAI chat completion with deployment: {_deploymentName}");
            builder.AddAzureOpenAIChatCompletion(_deploymentName, _endpoint, _apiKey);
        }

        public void ConfigureEmbeddings(IKernelBuilder builder)
        {
            Console.WriteLine($"🔧 Configuring Azure OpenAI embeddings with deployment: {_embeddingDeploymentName}");
            builder.AddAzureOpenAIEmbeddingGenerator(_embeddingDeploymentName, _endpoint, _apiKey);
        }

        public string GetProviderName() => "Azure OpenAI";

        public ValidationResult ValidateConfiguration()
        {
            var result = new ValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(_deploymentName))
                result.Errors.Add("DeploymentName is required");

            if (string.IsNullOrWhiteSpace(_endpoint))
                result.Errors.Add("Endpoint is required");

            if (!Uri.TryCreate(_endpoint, UriKind.Absolute, out _))
                result.Errors.Add("Endpoint must be a valid URI");

            if (string.IsNullOrWhiteSpace(_apiKey))
                result.Errors.Add("ApiKey is required");

            if (_apiKey.Length < 20)
                result.Warnings.Add("ApiKey seems too short, please verify");

            if (string.IsNullOrWhiteSpace(_embeddingDeploymentName))
                result.Errors.Add("EmbeddingDeploymentName is required");

            result.IsValid = !result.Errors.Any();
            return result;
        }
    }
}
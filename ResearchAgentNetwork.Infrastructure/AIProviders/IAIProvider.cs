using Microsoft.SemanticKernel;

namespace ResearchAgentNetwork.AIProviders
{
    public interface IAIProvider
    {
        void ConfigureKernel(IKernelBuilder builder);
        string GetProviderName();
        
        /// <summary>
        /// Configures the kernel with embedding generation support for semantic memory
        /// </summary>
        /// <param name="builder">The kernel builder to configure</param>
        void ConfigureEmbeddings(IKernelBuilder builder);
        
        /// <summary>
        /// Validates the provider configuration
        /// </summary>
        /// <returns>Validation result with any errors</returns>
        ValidationResult ValidateConfiguration();
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
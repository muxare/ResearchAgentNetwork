using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using ResearchAgentNetwork.SemanticMemory;

namespace ResearchAgentNetwork.Infrastructure.SemanticMemory;

public class EmbeddingService : IEmbeddingService
{
    private readonly Kernel _kernel;

    public EmbeddingService(Kernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        // Use SK's built embedding generator via kernel
        var generator = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        var vec = await generator.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        return vec.ToArray();
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> inputs, CancellationToken cancellationToken = default)
    {
        var generator = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        var list = inputs.ToList();
        var embeddings = await generator.GenerateEmbeddingsAsync(list, cancellationToken: cancellationToken);
        return embeddings.Select(e => e.ToArray()).ToList();
    }
}


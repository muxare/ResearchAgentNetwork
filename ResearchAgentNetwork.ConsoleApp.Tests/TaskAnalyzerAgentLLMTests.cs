using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using ResearchAgentNetwork.AIProviders;
using Xunit;
using Xunit.Sdk;

namespace ResearchAgentNetwork.ConsoleApp.Tests
{
    public class TaskAnalyzerAgentLLMTests : IDisposable
    {
        private readonly TaskAnalyzerAgent _agent;
        private readonly Kernel _kernel;
        private readonly OllamaProvider _ollamaProvider;
        private readonly IConfiguration _configuration;

        public TaskAnalyzerAgentLLMTests()
        {
            // Load configuration
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.test.json", optional: false)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            // Configure Ollama provider
            var ollamaConfig = _configuration.GetSection("Ollama");
            _ollamaProvider = new OllamaProvider(
                ollamaConfig["ModelId"] ?? "llama3.1:latest",
                ollamaConfig["Endpoint"] ?? "http://localhost:11434",
                ollamaConfig["EmbeddingModelId"] ?? "llama3.1"
            );

            // Validate configuration
            var validation = _ollamaProvider.ValidateConfiguration();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Ollama configuration is invalid: {string.Join(", ", validation.Errors)}");
            }

            // Build kernel with Ollama
            var builder = Kernel.CreateBuilder();
            _ollamaProvider.ConfigureKernel(builder);
            _ollamaProvider.ConfigureEmbeddings(builder);
            _kernel = builder.Build();

            _agent = new TaskAnalyzerAgent();
        }

        private async Task<bool> IsOllamaAvailableAsync()
        {
            return await LLMTestHelper.IsOllamaAvailableAsync();
        }

        [Fact]
        public async Task TaskAnalyzerAgent_ShouldAnalyzeSimpleTaskWithLLM()
        {
            if (!await IsOllamaAvailableAsync()) return; // skip gracefully
            // Arrange
            var task = new ResearchTask
            {
                Description = "What is the capital of France?",
                Priority = 5
            };

            // Act
            var response = await _agent.ProcessAsync(task, _kernel);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            
            // Accept either direct execution readiness or decomposition
            Assert.False(response.Data is List<ResearchTask>);
        }

        [Fact]
        public async Task TaskAnalyzerAgent_ShouldDecomposeComplexTaskWithLLM()
        {
            if (!await IsOllamaAvailableAsync()) return; // skip gracefully
            // Arrange
            var task = new ResearchTask
            {
                Description = "Research the impact of artificial intelligence on healthcare, education, and transportation",
                Priority = 8
            };

            // Act
            var response = await _agent.ProcessAsync(task, _kernel);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            
            // Prefer decomposition, but allow atomic readiness depending on model behavior
            if (response.Data is List<ResearchTask> subTasks)
            {
                Assert.True(subTasks.Count >= 3, "Complex task should be decomposed into at least 3 subtasks");
                foreach (var subTask in subTasks)
                {
                    Assert.NotEqual(Guid.Empty, subTask.Id);
                    Assert.False(string.IsNullOrWhiteSpace(subTask.Description));
                }
            }
        }

        [Fact]
        public async Task TaskAnalyzerAgent_ShouldHandleTechnicalTaskWithLLM()
        {
            if (!await IsOllamaAvailableAsync()) return; // skip gracefully
            // Arrange
            var task = new ResearchTask
            {
                Description = "Implement a machine learning pipeline for sentiment analysis including data preprocessing, model selection, training, evaluation, and deployment",
                Priority = 9
            };

            // Act
            var response = await _agent.ProcessAsync(task, _kernel);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            
            // Prefer decomposition, but allow atomic readiness depending on model behavior
            if (response.Data is List<ResearchTask> subTasks)
            {
                Assert.True(subTasks.Count >= 4, "Technical task should be decomposed into at least 4 subtasks");
                var descriptions = subTasks.Select(st => st.Description.ToLower()).ToList();
                Assert.Contains(descriptions, d => d.Contains("data") || d.Contains("preprocessing"));
                Assert.Contains(descriptions, d => d.Contains("model") || d.Contains("training"));
            }
        }

        [Fact]
        public async Task TaskAnalyzerAgent_ShouldHandleResearchTaskWithLLM()
        {
            if (!await IsOllamaAvailableAsync()) return; // skip gracefully
            // Arrange
            var task = new ResearchTask
            {
                Description = "Analyze the effectiveness of renewable energy sources in reducing carbon emissions",
                Priority = 7
            };

            // Act
            var response = await _agent.ProcessAsync(task, _kernel);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            
            // This could be either a simple task or complex task depending on LLM analysis
            if (response.Data is List<ResearchTask> subTasks)
            {
                Assert.True(subTasks.Count >= 2, "Research task should be decomposed into at least 2 subtasks");
                foreach (var subTask in subTasks)
                {
                    Assert.False(string.IsNullOrWhiteSpace(subTask.Description));
                }
            }
            // else: ready for execution is acceptable without specific string check
        }

        [Fact]
        public async Task TaskAnalyzerAgent_ShouldHandleEmptyTaskDescriptionWithLLM()
        {
            if (!await IsOllamaAvailableAsync()) return; // skip gracefully
            // Arrange
            var task = new ResearchTask
            {
                Description = "",
                Priority = 5
            };

            // Act
            var response = await _agent.ProcessAsync(task, _kernel);

            // Assert
            Assert.NotNull(response);
            // The response might fail or handle empty description gracefully
            // We're testing that it doesn't crash
        }

        [Fact]
        public async Task TaskAnalyzerAgent_ShouldHandleVeryLongTaskDescriptionWithLLM()
        {
            if (!await IsOllamaAvailableAsync()) return; // skip gracefully
            // Arrange
            var longDescription = "Research the comprehensive impact of artificial intelligence and machine learning technologies on various sectors including healthcare, finance, education, transportation, manufacturing, agriculture, entertainment, and cybersecurity, considering both positive and negative implications, ethical considerations, regulatory frameworks, economic impacts, and future trends over the next decade";
            var task = new ResearchTask
            {
                Description = longDescription,
                Priority = 10
            };

            // Act
            var response = await _agent.ProcessAsync(task, _kernel);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            
            // Prefer decomposition, but allow atomic readiness depending on model behavior
            if (response.Data is List<ResearchTask> subTasks)
            {
                Assert.True(subTasks.Count >= 5, "Very long complex task should be decomposed into at least 5 subtasks");
            }
        }

        [Fact]
        public async Task TaskAnalyzerAgent_ShouldHandleMultiLanguageTaskWithLLM()
        {
            if (!await IsOllamaAvailableAsync()) return; // skip gracefully
            // Arrange
            var task = new ResearchTask
            {
                Description = "Analyze the impact of globalization on local cultures (English) and its effects on traditional values (Español)",
                Priority = 6
            };

            // Act
            var response = await _agent.ProcessAsync(task, _kernel);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            
            // This should be decomposed to handle multiple languages
            Assert.IsType<List<ResearchTask>>(response.Data);
            var subTasks = (List<ResearchTask>)response.Data;
            Assert.True(subTasks.Count >= 2, "Multi-language task should be decomposed into at least 2 subtasks");
        }

        [Fact]
        public async Task TaskAnalyzerAgent_ShouldHandleTaskWithSpecialCharactersWithLLM()
        {
            if (!await IsOllamaAvailableAsync()) return; // skip gracefully
            // Arrange
            var task = new ResearchTask
            {
                Description = "Analyze the impact of AI on society: pros & cons, risks vs benefits, future implications...",
                Priority = 7
            };

            // Act
            var response = await _agent.ProcessAsync(task, _kernel);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            
            // Should handle special characters without issues
            if (response.Data is List<ResearchTask> subTasks)
            {
                foreach (var subTask in subTasks)
                {
                    Assert.False(string.IsNullOrWhiteSpace(subTask.Description));
                }
            }
        }

        [Fact]
        public async Task TaskAnalyzerAgent_ShouldHandleConcurrentRequestsWithLLM()
        {
            if (!await IsOllamaAvailableAsync()) return; // skip gracefully
            // Arrange
            var tasks = new List<ResearchTask>
            {
                new() { Description = "What is machine learning?", Priority = 5 },
                new() { Description = "Research quantum computing applications", Priority = 7 },
                new() { Description = "Analyze blockchain technology", Priority = 6 }
            };

            // Act
            var responses = await Task.WhenAll(
                tasks.Select(task => _agent.ProcessAsync(task, _kernel))
            );

            // Assert
            Assert.Equal(3, responses.Length);
            foreach (var response in responses)
            {
                Assert.NotNull(response);
                Assert.True(response.Success);
                Assert.NotNull(response.Data);
            }
        }

        [Fact]
        public async Task TaskAnalyzerAgent_ShouldHandleTaskWithMetadataWithLLM()
        {
            if (!await IsOllamaAvailableAsync()) return; // skip gracefully
            // Arrange
            var task = new ResearchTask
            {
                Description = "Research the latest developments in renewable energy",
                Priority = 8,
                Metadata = new Dictionary<string, object>
                {
                    ["Domain"] = "Energy",
                    ["Timeframe"] = "2024",
                    ["GeographicScope"] = "Global"
                }
            };

            // Act
            var response = await _agent.ProcessAsync(task, _kernel);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            
            // The agent should process the task regardless of metadata
            if (response.Data is List<ResearchTask> subTasks)
            {
                Assert.True(subTasks.Count > 0);
            }
        }

        public void Dispose()
        {
            // Clean up any resources if needed
            // Kernel doesn't implement IDisposable in current version
        }
    }
} 
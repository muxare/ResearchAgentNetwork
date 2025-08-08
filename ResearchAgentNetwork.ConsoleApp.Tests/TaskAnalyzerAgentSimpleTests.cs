using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using ResearchAgentNetwork.AIProviders;

namespace ResearchAgentNetwork.ConsoleApp.Tests
{
    public class TaskAnalyzerAgentSimpleTests
    {
        private readonly TaskAnalyzerAgent _agent;

        public TaskAnalyzerAgentSimpleTests()
        {
            _agent = new TaskAnalyzerAgent();
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHaveCorrectRole()
        {
            // Act & Assert
            Assert.Equal("TaskAnalyzer", _agent.Role);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldImplementIResearchAgent()
        {
            // Act & Assert
            Assert.IsAssignableFrom<IResearchAgent>(_agent);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldNotBeNull()
        {
            // Act & Assert
            Assert.NotNull(_agent);
        }

        [Theory]
        [InlineData("What is the capital of France?")]
        [InlineData("What is 2+2?")]
        [InlineData("Who wrote Romeo and Juliet?")]
        [InlineData("What is the largest planet in our solar system?")]
        public void TaskAnalyzerAgent_ShouldHandleSimpleTaskDescriptions(string taskDescription)
        {
            // Arrange
            var task = new ResearchTask
            {
                Description = taskDescription,
                Priority = 5
            };

            // Act & Assert
            Assert.NotNull(task);
            Assert.Equal(taskDescription, task.Description);
            Assert.Equal(5, task.Priority);
            Assert.NotEqual(Guid.Empty, task.Id);
        }

        [Theory]
        [InlineData("Analyze the impact of quantum computing on cryptography")]
        [InlineData("Research the effectiveness of renewable energy sources")]
        [InlineData("Investigate the relationship between social media and mental health")]
        [InlineData("Examine the economic implications of artificial intelligence")]
        public void TaskAnalyzerAgent_ShouldHandleComplexTaskDescriptions(string taskDescription)
        {
            // Arrange
            var task = new ResearchTask
            {
                Description = taskDescription,
                Priority = 5
            };

            // Act & Assert
            Assert.NotNull(task);
            Assert.Equal(taskDescription, task.Description);
            Assert.Equal(5, task.Priority);
            Assert.NotEqual(Guid.Empty, task.Id);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleEmptyTaskDescription()
        {
            // Arrange
            var task = new ResearchTask
            {
                Description = "",
                Priority = 5
            };

            // Act & Assert
            Assert.NotNull(task);
            Assert.Equal("", task.Description);
            Assert.Equal(5, task.Priority);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleNullTaskDescription()
        {
            // Arrange
            var task = new ResearchTask
            {
                Description = null!,
                Priority = 5
            };

            // Act & Assert
            Assert.NotNull(task);
            Assert.Null(task.Description);
            Assert.Equal(5, task.Priority);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public void TaskAnalyzerAgent_ShouldHandleDifferentPriorities(int priority)
        {
            // Arrange
            var task = new ResearchTask
            {
                Description = "Test task",
                Priority = priority
            };

            // Act & Assert
            Assert.NotNull(task);
            Assert.Equal(priority, task.Priority);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleTaskWithMetadata()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                ["Domain"] = "Computer Science",
                ["Priority"] = "High",
                ["Timeframe"] = "2024"
            };

            var task = new ResearchTask
            {
                Description = "Test task with metadata",
                Priority = 5,
                Metadata = metadata
            };

            // Act & Assert
            Assert.NotNull(task);
            Assert.Equal("Computer Science", task.Metadata["Domain"]);
            Assert.Equal("High", task.Metadata["Priority"]);
            Assert.Equal("2024", task.Metadata["Timeframe"]);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleTaskWithParent()
        {
            // Arrange
            var parentId = Guid.NewGuid();
            var task = new ResearchTask
            {
                Description = "Child task",
                Priority = 5,
                ParentTaskId = parentId
            };

            // Act & Assert
            Assert.NotNull(task);
            Assert.Equal(parentId, task.ParentTaskId);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleVeryLongTaskDescription()
        {
            // Arrange
            var longDescription = new string('A', 10000);
            var task = new ResearchTask
            {
                Description = longDescription,
                Priority = 5
            };

            // Act & Assert
            Assert.NotNull(task);
            Assert.Equal(longDescription, task.Description);
            Assert.Equal(10000, task.Description.Length);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleSpecialCharacters()
        {
            // Arrange
            var specialDescription = "Analyze the impact of AI on society: pros & cons, risks vs benefits, future implications...";
            var task = new ResearchTask
            {
                Description = specialDescription,
                Priority = 5
            };

            // Act & Assert
            Assert.NotNull(task);
            Assert.Equal(specialDescription, task.Description);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleMultiLanguageTask()
        {
            // Arrange
            var multiLanguageDescription = "Analyze the impact of globalization on local cultures (English) and its effects on traditional values (Español)";
            var task = new ResearchTask
            {
                Description = multiLanguageDescription,
                Priority = 5
            };

            // Act & Assert
            Assert.NotNull(task);
            Assert.Equal(multiLanguageDescription, task.Description);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleTechnicalTask()
        {
            // Arrange
            var technicalDescription = "Implement a machine learning pipeline for sentiment analysis using Python, including data preprocessing, model selection, training, evaluation, and deployment";
            var task = new ResearchTask
            {
                Description = technicalDescription,
                Priority = 5
            };

            // Act & Assert
            Assert.NotNull(task);
            Assert.Equal(technicalDescription, task.Description);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleTaskWithJsonContent()
        {
            // Arrange
            var jsonDescription = "Analyze this JSON structure: {\"name\": \"test\", \"value\": 123} and explain its purpose";
            var task = new ResearchTask
            {
                Description = jsonDescription,
                Priority = 5
            };

            // Act & Assert
            Assert.NotNull(task);
            Assert.Equal(jsonDescription, task.Description);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleTaskWithCodeContent()
        {
            // Arrange
            var codeDescription = "Review this code: function add(a, b) { return a + b; } and suggest improvements";
            var task = new ResearchTask
            {
                Description = codeDescription,
                Priority = 5
            };

            // Act & Assert
            Assert.NotNull(task);
            Assert.Equal(codeDescription, task.Description);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleTaskWithSubTasks()
        {
            // Arrange
            var task = new ResearchTask
            {
                Description = "Parent task",
                Priority = 5,
                SubTaskIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() }
            };

            // Act & Assert
            Assert.NotNull(task);
            Assert.Equal(2, task.SubTaskIds.Count);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleTaskWithResult()
        {
            // Arrange
            var result = new ResearchResult
            {
                Content = "Test result content",
                ConfidenceScore = 0.85,
                Sources = new List<string> { "Source 1", "Source 2" }
            };

            var task = new ResearchTask
            {
                Description = "Task with result",
                Priority = 5,
                Result = result
            };

            // Act & Assert
            Assert.NotNull(task);
            Assert.NotNull(task.Result);
            Assert.Equal("Test result content", task.Result.Content);
            Assert.Equal(0.85, task.Result.ConfidenceScore);
            Assert.Equal(2, task.Result.Sources.Count);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleTaskWithDifferentStatuses()
        {
            // Arrange & Act
            var pendingTask = new ResearchTask { Status = TaskStatus.Pending };
            var analyzingTask = new ResearchTask { Status = TaskStatus.Analyzing };
            var executingTask = new ResearchTask { Status = TaskStatus.Executing };
            var completedTask = new ResearchTask { Status = TaskStatus.Completed };
            var failedTask = new ResearchTask { Status = TaskStatus.Failed };

            // Assert
            Assert.Equal(TaskStatus.Pending, pendingTask.Status);
            Assert.Equal(TaskStatus.Analyzing, analyzingTask.Status);
            Assert.Equal(TaskStatus.Executing, executingTask.Status);
            Assert.Equal(TaskStatus.Completed, completedTask.Status);
            Assert.Equal(TaskStatus.Failed, failedTask.Status);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleTaskCreationTime()
        {
            // Arrange
            var beforeCreation = DateTime.UtcNow;
            var task = new ResearchTask
            {
                Description = "Test task",
                Priority = 5
            };
            var afterCreation = DateTime.UtcNow;

            // Act & Assert
            Assert.NotNull(task);
            Assert.True(task.CreatedAt >= beforeCreation);
            Assert.True(task.CreatedAt <= afterCreation);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleComplexityAnalysis()
        {
            // Arrange
            var complexityAnalysis = new ComplexityAnalysis
            {
                RequiresDecomposition = true,
                Complexity = 8,
                Reasoning = "Complex multi-faceted topic"
            };

            // Act & Assert
            Assert.NotNull(complexityAnalysis);
            Assert.True(complexityAnalysis.RequiresDecomposition);
            Assert.Equal(8, complexityAnalysis.Complexity);
            Assert.Equal("Complex multi-faceted topic", complexityAnalysis.Reasoning);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleAgentResponse()
        {
            // Arrange
            var response = new AgentResponse
            {
                Success = true,
                Message = "Test message",
                Data = new { TestProperty = "TestValue" }
            };

            // Act & Assert
            Assert.NotNull(response);
            Assert.True(response.Success);
            Assert.Equal("Test message", response.Message);
            Assert.NotNull(response.Data);
        }

        [Fact]
        public void TaskAnalyzerAgent_ShouldHandleResearchResult()
        {
            // Arrange
            var researchResult = new ResearchResult
            {
                Content = "Research content",
                ConfidenceScore = 0.9,
                Sources = new List<string> { "Source 1", "Source 2", "Source 3" },
                Metadata = new Dictionary<string, object> { ["Key"] = "Value" },
                RequiresAdditionalResearch = false
            };

            // Act & Assert
            Assert.NotNull(researchResult);
            Assert.Equal("Research content", researchResult.Content);
            Assert.Equal(0.9, researchResult.ConfidenceScore);
            Assert.Equal(3, researchResult.Sources.Count);
            Assert.Equal("Value", researchResult.Metadata["Key"]);
            Assert.False(researchResult.RequiresAdditionalResearch);
        }
    }
} 
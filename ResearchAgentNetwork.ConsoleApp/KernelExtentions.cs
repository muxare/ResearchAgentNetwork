using Microsoft.SemanticKernel;
using System.Text.Json;

namespace ResearchAgentNetwork
{
    public static class KernelExtensions
    {
        /// <summary>
        /// Executes a prompt and returns the result as a strongly-typed object.
        /// The prompt will be enhanced with JSON schema instructions to ensure structured output.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the JSON response into</typeparam>
        /// <param name="kernel">The kernel instance</param>
        /// <param name="prompt">The base prompt to execute</param>
        /// <param name="arguments">Optional arguments for the prompt</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The deserialized object of type T</returns>
        public static async Task<T> WithStructuredOutput<T>(
            this Kernel kernel,
            string prompt,
            KernelArguments? arguments = null,
            CancellationToken cancellationToken = default)
        {
            // Generate JSON schema from the target type
            var jsonSchema = JsonStuff.GenerateJsonSchemaFromClass<T>();
            
            // Enhance the prompt with JSON schema instructions
            var enhancedPrompt = $@"{prompt}

IMPORTANT: You must respond with valid JSON that conforms to this exact schema:

{jsonSchema}

Ensure your response is valid JSON and matches the schema structure exactly. Do not include any text before or after the JSON.";

            // Execute the prompt
            var result = await kernel.InvokePromptAsync(enhancedPrompt, arguments, cancellationToken: cancellationToken);
            var raw = result.GetValue<string>() ?? "{}";

            // Attempt to trim code fences or prose around JSON if model ignored instructions
            var jsonResponse = ExtractJsonPayload(raw);

            try
            {
                // Deserialize the JSON response
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var deserialized = JsonSerializer.Deserialize<T>(jsonResponse, options);
                
                if (deserialized == null)
                {
                    throw new InvalidOperationException($"Failed to deserialize JSON response into type {typeof(T).Name}");
                }

                return deserialized;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to parse LLM response as JSON for type {typeof(T).Name}. " +
                    $"Response: {raw}. " +
                    $"Error: {ex.Message}", ex);
            }
        }

        private static string ExtractJsonPayload(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "{}";

            // If already looks like JSON, return as-is
            var first = text.TrimStart();
            if (first.StartsWith("[") || first.StartsWith("{")) return first;

            // Try to locate the first JSON block between backticks or braces
            int startArray = text.IndexOf('[');
            int startObject = text.IndexOf('{');
            int start = -1;
            char open = '\0';
            char close = '\0';
            if (startArray >= 0 && (startObject < 0 || startArray < startObject)) { start = startArray; open = '['; close = ']'; }
            else if (startObject >= 0) { start = startObject; open = '{'; close = '}'; }

            if (start < 0) return text; // fallback

            int depth = 0;
            for (int i = start; i < text.Length; i++)
            {
                if (text[i] == open) depth++;
                else if (text[i] == close) depth--;
                if (depth == 0 && i > start)
                {
                    return text.Substring(start, i - start + 1);
                }
            }

            return text.Substring(start); // best effort
        }

        /// <summary>
        /// Executes a prompt with structured output and provides detailed error information.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the JSON response into</typeparam>
        /// <param name="kernel">The kernel instance</param>
        /// <param name="prompt">The base prompt to execute</param>
        /// <param name="arguments">Optional arguments for the prompt</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A result containing either the deserialized object or error information</returns>
        public static async Task<StructuredOutputResult<T>> WithStructuredOutputSafe<T>(
            this Kernel kernel,
            string prompt,
            KernelArguments? arguments = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await kernel.WithStructuredOutput<T>(prompt, arguments, cancellationToken);
                return StructuredOutputResult<T>.Success(result);
            }
            catch (Exception ex)
            {
                return StructuredOutputResult<T>.Failure(ex.Message, ex);
            }
        }

        /// <summary>
        /// Executes a prompt with structured output and retries on failure.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the JSON response into</typeparam>
        /// <param name="kernel">The kernel instance</param>
        /// <param name="prompt">The base prompt to execute</param>
        /// <param name="maxRetries">Maximum number of retry attempts</param>
        /// <param name="arguments">Optional arguments for the prompt</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The deserialized object of type T</returns>
        public static async Task<T> WithStructuredOutputRetry<T>(
            this Kernel kernel,
            string prompt,
            int maxRetries = 3,
            KernelArguments? arguments = null,
            CancellationToken cancellationToken = default)
        {
            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return await kernel.WithStructuredOutput<T>(prompt, arguments, cancellationToken);
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    lastException = ex;
                    
                    // Add retry context to the prompt for the next attempt
                    var retryPrompt = $@"{prompt}

Previous attempt failed with error: {ex.Message}

Please ensure your response is valid JSON and try again.";

                    // Wait before retry with exponential backoff
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    await Task.Delay(delay, cancellationToken);
                    
                    // Update the prompt for the next attempt
                    prompt = retryPrompt;
                }
            }

            throw new InvalidOperationException(
                $"Failed to get structured output after {maxRetries} attempts. " +
                $"Last error: {lastException?.Message}", lastException);
        }
    }

    /// <summary>
    /// Result wrapper for structured output operations that may fail.
    /// </summary>
    /// <typeparam name="T">The type of the structured output</typeparam>
    public class StructuredOutputResult<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public string? ErrorMessage { get; }
        public Exception? Exception { get; }

        private StructuredOutputResult(bool isSuccess, T? value, string? errorMessage = null, Exception? exception = null)
        {
            IsSuccess = isSuccess;
            Value = value;
            ErrorMessage = errorMessage;
            Exception = exception;
        }

        public static StructuredOutputResult<T> Success(T value)
        {
            return new StructuredOutputResult<T>(true, value);
        }

        public static StructuredOutputResult<T> Failure(string errorMessage, Exception? exception = null)
        {
            return new StructuredOutputResult<T>(false, default, errorMessage, exception);
        }

        /// <summary>
        /// Throws an exception if the operation failed.
        /// </summary>
        public T GetValueOrThrow()
        {
            if (!IsSuccess)
            {
                throw new InvalidOperationException(ErrorMessage, Exception);
            }
            return Value!;
        }
    }
}
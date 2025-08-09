using Microsoft.SemanticKernel;
using System.Text.Json;

namespace ResearchAgentNetwork
{
    public static class KernelExtensions
    {
        public static bool EnablePromptLogging { get; set; } = false;
        public static async Task<T> WithStructuredOutput<T>(this Kernel kernel, string prompt, KernelArguments? arguments = null, CancellationToken cancellationToken = default)
        {
            var jsonSchema = JsonStuff.GenerateJsonSchemaFromClass<T>();
            var enhancedPrompt = $@"{prompt}

IMPORTANT: You must respond with valid JSON that conforms to this exact schema:

{jsonSchema}

Ensure your response is valid JSON and matches the schema structure exactly. Do not include any text before or after the JSON.";

            if (EnablePromptLogging)
            {
                Console.WriteLine("——— LLM Prompt ———");
                Console.WriteLine(enhancedPrompt);
                Console.WriteLine("———————————————");
            }

            var result = await kernel.InvokePromptAsync(enhancedPrompt, arguments, cancellationToken: cancellationToken);
            var raw = result.GetValue<string>() ?? "{}";

            if (EnablePromptLogging)
            {
                Console.WriteLine("——— LLM Response ———");
                Console.WriteLine(raw);
                Console.WriteLine("———————————————");
            }

            var jsonResponse = ExtractJsonPayload(raw);

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var deserialized = JsonSerializer.Deserialize<T>(jsonResponse, options);
                if (deserialized == null)
                {
                    throw new InvalidOperationException($"Failed to deserialize JSON response into type {typeof(T).Name}");
                }
                return deserialized;
            }
            catch (JsonException ex)
            {
                try
                {
                    var repaired = RepairInvalidStringLiterals(jsonResponse);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    var deserialized = JsonSerializer.Deserialize<T>(repaired, options);
                    if (deserialized != null) return deserialized;
                }
                catch { }

                throw new InvalidOperationException($"Failed to parse LLM response as JSON for type {typeof(T).Name}. Response: {raw}. Error: {ex.Message}", ex);
            }
        }

        private static string ExtractJsonPayload(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "{}";
            var first = text.TrimStart();
            if (first.StartsWith("[") || first.StartsWith("{")) return first;
            int startArray = text.IndexOf('[');
            int startObject = text.IndexOf('{');
            int start = -1; char open = '\0'; char close = '\0';
            if (startArray >= 0 && (startObject < 0 || startArray < startObject)) { start = startArray; open = '['; close = ']'; }
            else if (startObject >= 0) { start = startObject; open = '{'; close = '}'; }
            if (start < 0) return text;
            int depth = 0;
            for (int i = start; i < text.Length; i++)
            {
                if (text[i] == open) depth++;
                else if (text[i] == close) depth--;
                if (depth == 0 && i > start) return text.Substring(start, i - start + 1);
            }
            return text.Substring(start);
        }

        public static async Task<StructuredOutputResult<T>> WithStructuredOutputSafe<T>(this Kernel kernel, string prompt, KernelArguments? arguments = null, CancellationToken cancellationToken = default)
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

        public static async Task<T> WithStructuredOutputRetry<T>(this Kernel kernel, string prompt, int maxRetries = 3, KernelArguments? arguments = null, CancellationToken cancellationToken = default)
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
                    var retryPrompt = $@"{prompt}

Previous attempt failed with error: {ex.Message}

Please ensure your response is valid JSON and try again.";
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    await Task.Delay(delay, cancellationToken);
                    prompt = retryPrompt;
                }
            }
            throw new InvalidOperationException($"Failed to get structured output after {maxRetries} attempts. Last error: {lastException?.Message}", lastException);
        }

        private static string RepairInvalidStringLiterals(string json)
        {
            var sb = new System.Text.StringBuilder(json.Length + 64);
            bool inString = false; bool escapeNext = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (escapeNext) { sb.Append(c); escapeNext = false; continue; }
                if (c == '\\') { sb.Append(c); if (inString) escapeNext = true; continue; }
                if (c == '"') { inString = !inString; sb.Append(c); continue; }
                if (inString)
                {
                    if (c == '\r' || c == '\n') { sb.Append("\\n"); continue; }
                    if (c == '\t') { sb.Append("\\t"); continue; }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }

    public class StructuredOutputResult<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public string? ErrorMessage { get; }
        public Exception? Exception { get; }

        private StructuredOutputResult(bool isSuccess, T? value, string? errorMessage = null, Exception? exception = null)
        {
            IsSuccess = isSuccess; Value = value; ErrorMessage = errorMessage; Exception = exception;
        }

        public static StructuredOutputResult<T> Success(T value) => new(true, value);
        public static StructuredOutputResult<T> Failure(string errorMessage, Exception? exception = null) => new(false, default, errorMessage, exception);
        public T GetValueOrThrow()
        {
            if (!IsSuccess) throw new InvalidOperationException(ErrorMessage, Exception);
            return Value!;
        }
    }
}
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace ResearchAgentNetwork;

public static class JsonStuff
{
    public static string GenerateJsonSchemaFromClass<T>()
    {
        JsonSerializerOptions options = JsonSerializerOptions.Default;
        JsonNode schema = options.GetJsonSchemaAsNode(typeof(T));
        return schema.ToString();
    }
}
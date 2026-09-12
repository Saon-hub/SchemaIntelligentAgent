using System.Text.Json.Serialization;

namespace SchemaComparisonAgent.Models;

public class SchemaComparisonResponse
{
    [JsonPropertyName("entity")]
    public string? Entity { get; set; }

    [JsonPropertyName("mappings")]
    public List<ColumnMapping> Mappings { get; set; } = new();
}


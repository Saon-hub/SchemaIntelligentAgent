using System.Text.Json.Serialization;

namespace SchemaComparisonAgent.Models;

public class SchemaComparisonRequest
{
    [JsonPropertyName("SourceSchema")]
    public object? SourceSchema { get; set; }

    [JsonPropertyName("TargetSchema")]
    public object? TargetSchema { get; set; }

    [JsonPropertyName("RecommendedMappings")]
    public object? RecommendedMappings { get; set; }
}
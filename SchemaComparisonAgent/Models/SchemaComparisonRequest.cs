using System.Text.Json.Serialization;

namespace SchemaComparisonAgent.Models;

public class SchemaComparisonRequest
{
    [JsonPropertyName("SourceSchema")]
    public string? SourceSchema { get; set; }

    [JsonPropertyName("TargetSchema")]
    public string? TargetSchema { get; set; }

    [JsonPropertyName("RecommendedMappings")]
    public object? RecommendedMappings { get; set; }
}
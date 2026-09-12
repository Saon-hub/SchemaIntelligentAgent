using System.Text.Json.Serialization;

namespace SchemaComparisonAgent.Models;

public class EntityMapping
{
    [JsonPropertyName("entity")]
    public string Entity { get; set; } = string.Empty;

    [JsonPropertyName("mappings")]
    public List<ColumnMapping> Mappings { get; set; } = new();
}

public class ColumnMapping
{
    [JsonPropertyName("Source_TableName")]
    public string SourceTableName { get; set; } = string.Empty;

    [JsonPropertyName("Target_TableName")]
    public string TargetTableName { get; set; } = string.Empty;

    [JsonPropertyName("Source_ColumnName")]
    public string SourceColumnName { get; set; } = string.Empty;

    [JsonPropertyName("Target_ColumnName")]
    public string TargetColumnName { get; set; } = string.Empty;
}
namespace QueryBuilderAgent.Models;

public class MappingDefinition
{
    public string? Entity { get; set; }

    public List<ColumnMapping> Mappings { get; set; } = new();
}
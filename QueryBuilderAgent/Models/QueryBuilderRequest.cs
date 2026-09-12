namespace QueryBuilderAgent.Models;

public class QueryBuilderRequest
{
    public MappingDefinition? Mapping { get; set; }

    public string? SourceSchema { get; set; }

    public string? TargetSchema { get; set; }
}
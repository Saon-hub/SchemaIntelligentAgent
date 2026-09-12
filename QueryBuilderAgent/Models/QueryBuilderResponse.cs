namespace QueryBuilderAgent.Models;

public class QueryBuilderResponse
{
    public string? Entity { get; set; }

    public string? SourceExtractionQuery { get; set; }

    public string? TargetInsertionQuery { get; set; }
}
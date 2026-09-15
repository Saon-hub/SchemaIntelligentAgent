namespace ETLAgent.Models;

public class MigrationRequest
{
    public string Entity { get; set; } = string.Empty;

    public string SourceExtractionQuery { get; set; } = string.Empty;

    public string TargetInsertionQuery { get; set; } = string.Empty;
}
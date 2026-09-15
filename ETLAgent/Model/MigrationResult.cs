namespace ETLAgent.Models;

public class MigrationResult
{
    public string Entity { get; set; } = string.Empty;

    public bool Success { get; set; }

    public int ExtractedRows { get; set; }

    public int InsertedRows { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? Error { get; set; }
}
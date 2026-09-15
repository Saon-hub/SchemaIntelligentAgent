namespace ETLAgent.Models;

public class MigrationResponse
{
    public bool Success { get; set; }

    public int TotalEntities { get; set; }

    public int SuccessfulEntities { get; set; }

    public int FailedEntities { get; set; }

    public List<MigrationResult> Results { get; set; } = new();
}
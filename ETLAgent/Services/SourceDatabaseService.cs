using MySqlConnector;

namespace ETLAgent.Services;

public class SourceDatabaseService
{
    public async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(
        string connectionString,
        string sql)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "Source database connection string cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException(
                "Source extraction query cannot be empty.");
        }

        await using var connection =
            new MySqlConnection(connectionString);

        await connection.OpenAsync();

        await using var command =
            new MySqlCommand(sql, connection);

        command.CommandTimeout = 300;

        await using var reader =
            await command.ExecuteReaderAsync();

        var rows =
            new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync())
        {
            var row =
                new Dictionary<string, object?>(
                    StringComparer.OrdinalIgnoreCase);

            for (int i = 0;
                 i < reader.FieldCount;
                 i++)
            {
                var columnName =
                    reader.GetName(i);

                var value =
                    reader.IsDBNull(i)
                        ? null
                        : reader.GetValue(i);

                row[columnName] = value;
            }

            rows.Add(row);
        }

        return rows;
    }
}
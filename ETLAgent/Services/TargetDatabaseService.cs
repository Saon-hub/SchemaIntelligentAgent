using System.Text.RegularExpressions;
using MySqlConnector;

namespace ETLAgent.Services;

public class TargetDatabaseService
{
    public async Task<int> InsertRowsAsync(
        string connectionString,
        string sql,
        List<Dictionary<string, object?>> rows)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "Target database connection string cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException(
                "Target insertion query cannot be empty.");
        }

        if (rows.Count == 0)
        {
            return 0;
        }

        await using var connection =
            new MySqlConnection(connectionString);

        await connection.OpenAsync();

        await using var transaction =
            await connection.BeginTransactionAsync();

        try
        {
            var parameterNames =
                ExtractParameterNames(sql);

            int insertedRows = 0;

            foreach (var row in rows)
            {
                await using var command =
                    new MySqlCommand(
                        sql,
                        connection,
                        transaction);

                command.CommandTimeout = 300;

                foreach (var parameterName
                         in parameterNames)
                {
                    var columnName =
                        parameterName.TrimStart(
                            '@',
                            ':',
                            '?');

                    object? value;

                    if (row.TryGetValue(
                            columnName,
                            out var exactValue))
                    {
                        value = exactValue;
                    }
                    else if (row.TryGetValue(
                                 parameterName,
                                 out var parameterValue))
                    {
                        value = parameterValue;
                    }
                    else
                    {
                        throw new Exception(
                            $"Source column '{columnName}' " +
                            $"was not found for target parameter " +
                            $"'{parameterName}'.");
                    }

                    command.Parameters.AddWithValue(
                        parameterName,
                        value ?? DBNull.Value);
                }

                await command.ExecuteNonQueryAsync();

                insertedRows++;
            }

            await transaction.CommitAsync();

            return insertedRows;
        }
        catch
        {
            await transaction.RollbackAsync();

            throw;
        }
    }

    private static List<string> ExtractParameterNames(
        string sql)
    {
        var matches =
            Regex.Matches(
                sql,
                @"[@:][A-Za-z_][A-Za-z0-9_]*");

        return matches
            .Select(match => match.Value)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
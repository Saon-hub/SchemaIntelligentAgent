using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using ETLAgent.Models;
using ETLAgent.Services;

[assembly: LambdaSerializer(
    typeof(
        Amazon.Lambda.Serialization.SystemTextJson
        .DefaultLambdaJsonSerializer))]

namespace ETLAgent;

public class Function
{
    private readonly SourceDatabaseService _sourceDatabase;
    private readonly TargetDatabaseService _targetDatabase;

    private readonly string _sourceConnectionString;
    private readonly string _targetConnectionString;

    public Function()
    {
        _sourceDatabase =
            new SourceDatabaseService();

        _targetDatabase =
            new TargetDatabaseService();

        _sourceConnectionString =
            Environment.GetEnvironmentVariable(
                "SOURCE_DB_CONNECTION_STRING")
            ?? throw new Exception(
                "SOURCE_DB_CONNECTION_STRING " +
                "environment variable is not configured.");

        _targetConnectionString =
            Environment.GetEnvironmentVariable(
                "TARGET_DB_CONNECTION_STRING")
            ?? throw new Exception(
                "TARGET_DB_CONNECTION_STRING " +
                "environment variable is not configured.");

      
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(
        APIGatewayProxyRequest request,
        ILambdaContext context)
    {
        try
        {
            context.Logger.LogInformation(
                "Migration Execution Lambda started.");

            // --------------------------------------------
            // Validate request
            // --------------------------------------------

            if (request == null ||
                string.IsNullOrWhiteSpace(
                    request.Body))
            {
                return CreateResponse(
                    400,
                    new
                    {
                        success = false,
                        message =
                            "Request body cannot be empty."
                    });
            }

            // --------------------------------------------
            // Deserialize request ARRAY
            // --------------------------------------------

            var migrationRequests =
                JsonSerializer.Deserialize<
                    List<MigrationRequest>>(
                        request.Body,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

            if (migrationRequests == null ||
                migrationRequests.Count == 0)
            {
                return CreateResponse(
                    400,
                    new
                    {
                        success = false,
                        message =
                            "Request must contain at least " +
                            "one migration item."
                    });
            }

            context.Logger.LogInformation(
                $"Received {migrationRequests.Count} " +
                $"migration items.");

            // --------------------------------------------
            // Results
            // --------------------------------------------

            var results =
                new List<MigrationResult>();

            // ============================================
            // ITERATE THROUGH EACH MIGRATION
            // ============================================

            for (int i = 0;
                 i < migrationRequests.Count;
                 i++)
            {
                var migrationRequest =
                    migrationRequests[i];

                context.Logger.LogInformation(
                    $"Processing migration " +
                    $"{i + 1}/{migrationRequests.Count} " +
                    $"Entity: {migrationRequest.Entity}");

                try
                {
                    ValidateRequest(
                        migrationRequest);

                    // ====================================
                    // STEP 1
                    // SOURCE DATABASE
                    // ====================================

                    context.Logger.LogInformation(
                        $"Executing source query for " +
                        $"entity: {migrationRequest.Entity}");

                    var sourceRows =
                        await _sourceDatabase
                            .ExecuteQueryAsync(
                                _sourceConnectionString,
                                migrationRequest
                                    .SourceExtractionQuery);

                    context.Logger.LogInformation(
                        $"Extracted {sourceRows.Count} " +
                        $"rows for " +
                        $"{migrationRequest.Entity}");

                    // ====================================
                    // STEP 2
                    // TARGET DATABASE
                    // ====================================

                    int insertedRows = 0;

                    if (sourceRows.Count > 0)
                    {
                        context.Logger.LogInformation(
                            $"Starting target insertion " +
                            $"for {migrationRequest.Entity}");

                        insertedRows =
                            await _targetDatabase
                                .InsertRowsAsync(
                                    _targetConnectionString,
                                    migrationRequest
                                        .TargetInsertionQuery,
                                    sourceRows);
                    }

                    context.Logger.LogInformation(
                        $"Inserted {insertedRows} " +
                        $"rows for " +
                        $"{migrationRequest.Entity}");

                    // ====================================
                    // SUCCESS
                    // ====================================

                    results.Add(
                        new MigrationResult
                        {
                            Entity =
                                migrationRequest.Entity,

                            Success = true,

                            ExtractedRows =
                                sourceRows.Count,

                            InsertedRows =
                                insertedRows,

                            Message =
                                "Migration completed successfully."
                        });
                }
                catch (Exception ex)
                {
                    // ====================================
                    // FAILURE
                    // ====================================

                    context.Logger.LogError(
                        $"Migration failed for " +
                        $"{migrationRequest.Entity}: " +
                        $"{ex}");

                    results.Add(
                        new MigrationResult
                        {
                            Entity =
                                migrationRequest.Entity,

                            Success = false,

                            ExtractedRows = 0,

                            InsertedRows = 0,

                            Message =
                                "Migration failed.",

                            Error =
                                ex.Message
                        });

                    // Continue with next item
                    context.Logger.LogInformation(
                        "Continuing with next migration item.");
                }
            }

            // --------------------------------------------
            // Calculate summary
            // --------------------------------------------

            var successfulEntities =
                results.Count(
                    x => x.Success);

            var failedEntities =
                results.Count(
                    x => !x.Success);

            var overallSuccess =
                failedEntities == 0;

            // --------------------------------------------
            // Final response
            // --------------------------------------------

            var response =
                new MigrationResponse
                {
                    Success =
                        overallSuccess,

                    TotalEntities =
                        migrationRequests.Count,

                    SuccessfulEntities =
                        successfulEntities,

                    FailedEntities =
                        failedEntities,

                    Results =
                        results
                };

            context.Logger.LogInformation(
                "Migration Execution Lambda completed.");

            return CreateResponse(
                overallSuccess ? 200 : 207,
                response);
        }
        catch (JsonException ex)
        {
            context.Logger.LogError(
                $"Invalid JSON request: {ex}");

            return CreateResponse(
                400,
                new
                {
                    success = false,
                    message =
                        "Invalid JSON request.",
                    error = ex.Message
                });
        }
        catch (Exception ex)
        {
            context.Logger.LogError(
                $"Unexpected error: {ex}");

            return CreateResponse(
                500,
                new
                {
                    success = false,
                    message =
                        "Migration execution failed.",
                    error = ex.Message
                });
        }
    }

    private static void ValidateRequest(
        MigrationRequest request)
    {
        if (request == null)
        {
            throw new ArgumentException(
                "Migration request cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(
                request.Entity))
        {
            throw new ArgumentException(
                "Entity is required.");
        }

        if (string.IsNullOrWhiteSpace(
                request.SourceExtractionQuery))
        {
            throw new ArgumentException(
                $"SourceExtractionQuery is required " +
                $"for entity '{request.Entity}'.");
        }

        if (string.IsNullOrWhiteSpace(
                request.TargetInsertionQuery))
        {
            throw new ArgumentException(
                $"TargetInsertionQuery is required " +
                $"for entity '{request.Entity}'.");
        }
    }

    private static APIGatewayProxyResponse CreateResponse(
        int statusCode,
        object body)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = statusCode,

            Headers =
                new Dictionary<string, string>
                {
                    ["Content-Type"] =
                        "application/json",

                    ["Access-Control-Allow-Origin"] =
                        "*",

                    ["Access-Control-Allow-Headers"] =
                        "Content-Type",

                    ["Access-Control-Allow-Methods"] =
                        "OPTIONS,POST"
                },

            Body =
                JsonSerializer.Serialize(
                    body,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy =
                            JsonNamingPolicy.CamelCase
                    })
        };
    }
}
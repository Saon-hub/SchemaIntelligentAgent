using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Text.Json;
using QueryBuilderAgent.Models;
using QueryBuilderAgent.Services;

[assembly: LambdaSerializer(
    typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer)
)]

namespace QueryBuilderAgent;

public class Function
{
    private readonly BedrockQueryService _queryService;

    public Function()
    {
        _queryService = new BedrockQueryService();
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(
        APIGatewayProxyRequest request,
        ILambdaContext context)
    {
        try
        {
            context.Logger.LogInformation(
                "QueryBuilderAgent request received.");

            if (string.IsNullOrWhiteSpace(request.Body))
            {
                return CreateResponse(
                    400,
                    new
                    {
                        error = "Request body is required."
                    });
            }

            QueryBuilderRequest? queryRequest;

            try
            {
                queryRequest =
                    JsonSerializer.Deserialize<QueryBuilderRequest>(
                        request.Body,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
            }
            catch (JsonException ex)
            {
                context.Logger.LogError(
                    $"Invalid JSON request: {ex}");

                return CreateResponse(
                    400,
                    new
                    {
                        error = "Invalid JSON request body."
                    });
            }

            if (queryRequest == null)
            {
                return CreateResponse(
                    400,
                    new
                    {
                        error = "Invalid request."
                    });
            }

            if (queryRequest.Mapping == null)
            {
                return CreateResponse(
                    400,
                    new
                    {
                        error = "Mapping is required."
                    });
            }

            if (string.IsNullOrWhiteSpace(
                queryRequest.Mapping.Entity))
            {
                return CreateResponse(
                    400,
                    new
                    {
                        error = "Mapping.entity is required."
                    });
            }

            if (queryRequest.Mapping.Mappings == null ||
                queryRequest.Mapping.Mappings.Count == 0)
            {
                return CreateResponse(
                    400,
                    new
                    {
                        error = "At least one column mapping is required."
                    });
            }

            if (string.IsNullOrWhiteSpace(
                queryRequest.SourceSchema))
            {
                return CreateResponse(
                    400,
                    new
                    {
                        error = "Source schema is required."
                    });
            }

            if (string.IsNullOrWhiteSpace(
                queryRequest.TargetSchema))
            {
                return CreateResponse(
                    400,
                    new
                    {
                        error = "Target schema is required."
                    });
            }

            context.Logger.LogInformation(
                $"Building queries for entity: " +
                $"{queryRequest.Mapping.Entity}");

            QueryBuilderResponse result =
                await _queryService.BuildQueriesAsync(
                    queryRequest);

            return CreateResponse(
                200,
                result);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(
                $"QueryBuilderAgent failed: {ex}");

            return CreateResponse(
                500,
                new
                {
                    error = "Query generation failed.",
                    message = ex.Message
                });
        }
    }

    private static APIGatewayProxyResponse CreateResponse(
        int statusCode,
        object body)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = statusCode,
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json"
            },
            Body = JsonSerializer.Serialize(
                body,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                })
        };
    }
}
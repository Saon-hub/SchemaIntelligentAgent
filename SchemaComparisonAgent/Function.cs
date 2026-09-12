using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using SchemaComparisonAgent.Models;
using SchemaComparisonAgent.Services;
using System.Text.Json;

[assembly: LambdaSerializer(
    typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SchemaComparisonAgent;

public class Function
{
    private readonly BedrockSchemaComparisonService _comparisonService;

    public Function()
    {
        _comparisonService =
            new BedrockSchemaComparisonService();
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(
        APIGatewayProxyRequest request,
        ILambdaContext context)
    {
        try
        {
            context.Logger.LogInformation(
                "SchemaComparisonAgent started.");

            if (string.IsNullOrWhiteSpace(request.Body))
            {
                return CreateResponse(
                    400,
                    new
                    {
                        error = "Request body is required."
                    });
            }

            var comparisonRequest =
                JsonSerializer.Deserialize<SchemaComparisonRequest>(
                    request.Body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (comparisonRequest == null)
            {
                return CreateResponse(
                    400,
                    new
                    {
                        error = "Invalid request body."
                    });
            }

            if (comparisonRequest.SourceSchema == null)
            {
                return CreateResponse(
                    400,
                    new
                    {
                        error = "SourceSchema is required."
                    });
            }

            if (comparisonRequest.TargetSchema == null)
            {
                return CreateResponse(
                    400,
                    new
                    {
                        error = "TargetSchema is required."
                    });
            }

            if (comparisonRequest.RecommendedMappings == null)
            {
                return CreateResponse(
                    400,
                    new
                    {
                        error = "RecommendedMappings is required."
                    });
            }

            context.Logger.LogInformation(
                "Calling Amazon Bedrock Nova Lite.");

            var entityMappings =
                await _comparisonService.CompareSchemasAsync(
                    comparisonRequest);

            context.Logger.LogInformation(
                $"Generated {entityMappings.Count} entities.");

            return CreateResponse(
                200,
                entityMappings);
        }
        catch (JsonException ex)
        {
            context.Logger.LogError(
                $"Invalid JSON: {ex.Message}");

            return CreateResponse(
                400,
                new
                {
                    error = "Invalid JSON request.",
                    message = ex.Message
                });
        }
        catch (Exception ex)
        {
            context.Logger.LogError(
                $"Schema comparison failed: {ex}");

            return CreateResponse(
                500,
                new
                {
                    error = "Schema comparison failed.",
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

            Body = JsonSerializer.Serialize(body),

            IsBase64Encoded = false
        };
    }
}
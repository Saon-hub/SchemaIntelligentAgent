using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using SchemaComparisonAgent.Models;
using System.Text;
using System.Text.Json;

namespace SchemaComparisonAgent.Services;

public class BedrockSchemaComparisonService
{
    private readonly IAmazonBedrockRuntime _bedrockClient;

    private static readonly string ModelId =
        Environment.GetEnvironmentVariable("BEDROCK_MODEL_ID")
        ?? "amazon.nova-lite-v1:0";

    public BedrockSchemaComparisonService()
    {
        _bedrockClient =
            new AmazonBedrockRuntimeClient();
    }

    public async Task<List<EntityMapping>> CompareSchemasAsync(
        SchemaComparisonRequest request)
    {
        var prompt = BuildPrompt(request);

        var requestBody = new
        {
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new[]
                    {
                        new
                        {
                            text = prompt
                        }
                    }
                }
            },

            inferenceConfig = new
            {
                maxTokens = 4000,
                temperature = 0.0
            }
        };

        var jsonRequest =
            JsonSerializer.Serialize(requestBody);

        var bedrockRequest = new InvokeModelRequest
        {
            ModelId = ModelId,

            ContentType = "application/json",

            Accept = "application/json",

            Body = new MemoryStream(
                Encoding.UTF8.GetBytes(jsonRequest))
        };

        var response =
            await _bedrockClient.InvokeModelAsync(
                bedrockRequest);

        using var reader =
            new StreamReader(response.Body);

        var responseJson =
            await reader.ReadToEndAsync();

        return ParseModelResponse(responseJson);
    }

    private static string BuildPrompt(
        SchemaComparisonRequest request)
    {
        var sourceSchema =
            JsonSerializer.Serialize(
                request.SourceSchema,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        var targetSchema =
            JsonSerializer.Serialize(
                request.TargetSchema,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        var recommendedMappings =
            JsonSerializer.Serialize(
                request.RecommendedMappings,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        var sb = new StringBuilder();

        sb.AppendLine("You are SchemaComparisonAgent.");
        sb.AppendLine();
        sb.AppendLine("Your task is to analyze the SOURCE database schema and TARGET");
        sb.AppendLine("database schema and create business-entity-level mappings.");
        sb.AppendLine();
        sb.AppendLine("SOURCE SCHEMA:");
        sb.AppendLine(sourceSchema);
        sb.AppendLine();
        sb.AppendLine("TARGET SCHEMA:");
        sb.AppendLine(targetSchema);
        sb.AppendLine();
        sb.AppendLine("RECOMMENDED MAPPINGS:");
        sb.AppendLine(recommendedMappings);
        sb.AppendLine();
        sb.AppendLine("Instructions:");
        sb.AppendLine();
        sb.AppendLine("1. Analyze the source and target schemas.");
        sb.AppendLine("2. Identify logical business entities.");
        sb.AppendLine("3. Determine the appropriate source-to-target table mappings.");
        sb.AppendLine("4. Determine the appropriate source-to-target column mappings.");
        sb.AppendLine("5. Use the recommended mappings as guidance.");
        sb.AppendLine("6. Do not invent tables or columns that do not exist in the supplied schemas.");
        sb.AppendLine("7. Group related mappings under the appropriate business entity.");
        sb.AppendLine("8. Each entity can contain multiple mappings.");
        sb.AppendLine("9. Return ONLY valid JSON.");
        sb.AppendLine("10. Do not return markdown.");
        sb.AppendLine("11. Do not return ```json.");
        sb.AppendLine("12. Do not add explanations before or after the JSON.");
        sb.AppendLine();
        sb.AppendLine("The response must have EXACTLY this structure:");
        sb.AppendLine();
        sb.AppendLine("[");
        sb.AppendLine("  { \"entity\": \"User\",");
        sb.AppendLine("    \"mappings\": [");
        sb.AppendLine("      {");
        sb.AppendLine("        \"Source_TableName\": \"Customer\",");
        sb.AppendLine("        \"Target_TableName\": \"Client\",");
        sb.AppendLine("        \"Source_ColumnName\": \"CustomerName\",");
        sb.AppendLine("        \"Target_ColumnName\": \"ClientName\"");
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        sb.AppendLine("  }");
        sb.AppendLine("]");
        sb.AppendLine();
        sb.AppendLine("Important:");
        sb.AppendLine();
        sb.AppendLine("- The root JSON element MUST be an array.");
        sb.AppendLine("- Each array element MUST contain \"entity\" and \"mappings\".");
        sb.AppendLine("- \"mappings\" MUST be an array.");
        sb.AppendLine("- Each mapping MUST contain exactly:");
        sb.AppendLine("  \"Source_TableName\",");
        sb.AppendLine("  \"Target_TableName\",");
        sb.AppendLine("  \"Source_ColumnName\",");
        sb.AppendLine("  \"Target_ColumnName\".");
        sb.AppendLine("- Do NOT return a wrapper object.");
        sb.AppendLine("- Do NOT return \"entities\".");
        sb.AppendLine("- Do NOT return \"Mappings\" at the root level.");
        sb.AppendLine("- Return ONLY the JSON array.");

        return sb.ToString();
    }

    private static List<EntityMapping> ParseModelResponse(
        string responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            throw new InvalidOperationException(
                "Bedrock returned an empty response.");
        }

        using var document =
            JsonDocument.Parse(responseJson);

        var root =
            document.RootElement;

        if (!root.TryGetProperty(
                "output",
                out var output))
        {
            throw new InvalidOperationException(
                "Bedrock response does not contain 'output'.");
        }

        if (!output.TryGetProperty(
                "message",
                out var message))
        {
            throw new InvalidOperationException(
                "Bedrock response does not contain 'message'.");
        }

        if (!message.TryGetProperty(
                "content",
                out var content))
        {
            throw new InvalidOperationException(
                "Bedrock response does not contain 'content'.");
        }

        if (content.ValueKind != JsonValueKind.Array ||
            content.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                "Bedrock returned an empty content array.");
        }

        var modelOutput =
            content[0]
                .GetProperty("text")
                .GetString();

        if (string.IsNullOrWhiteSpace(modelOutput))
        {
            throw new InvalidOperationException(
                "Bedrock returned empty model output.");
        }

        modelOutput =
            modelOutput.Trim();

        // Remove markdown fences if the model returns them.
        if (modelOutput.StartsWith("```"))
        {
            var firstNewLine =
                modelOutput.IndexOf('\n');

            if (firstNewLine >= 0)
            {
                modelOutput =
                    modelOutput[(firstNewLine + 1)..];
            }

            var closingFence =
                modelOutput.LastIndexOf("```");

            if (closingFence >= 0)
            {
                modelOutput =
                    modelOutput[..closingFence];
            }

            modelOutput =
                modelOutput.Trim();
        }

        if (!modelOutput.StartsWith("["))
        {
            throw new InvalidOperationException(
                "Bedrock did not return a JSON array.");
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var result =
            JsonSerializer.Deserialize<List<EntityMapping>>(
                modelOutput,
                options);

        if (result == null)
        {
            throw new InvalidOperationException(
                "Unable to deserialize Bedrock response.");
        }

        return result;
    }
}
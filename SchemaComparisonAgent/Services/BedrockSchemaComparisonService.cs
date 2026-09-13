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
        /*
         * SourceSchema and TargetSchema are database schema / DDL text.
         *
         * IMPORTANT:
         * Do NOT JSON serialize these values.
         *
         * They should be sent to Bedrock exactly as database
         * schema definitions.
         */
        var sourceSchema =
            request.SourceSchema?.Trim();

        var targetSchema =
            request.TargetSchema?.Trim();

        /*
         * RecommendedMappings contains only source-side
         * tables/entities.
         *
         * Example:
         *
         * [
         *   {
         *     "sourceTable": "Customer"
         *   },
         *   {
         *     "sourceTable": "Orders"
         *   }
         * ]
         *
         * The target table is intentionally NOT supplied.
         */
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

        sb.AppendLine(
            "Your task is to analyze the SOURCE database schema and TARGET database schema and create business-entity-level source-to-target mappings.");

        sb.AppendLine();

        // ---------------------------------------------------------
        // SOURCE SCHEMA
        // ---------------------------------------------------------

        sb.AppendLine("SOURCE DATABASE SCHEMA:");
        sb.AppendLine("=======================");
        sb.AppendLine(sourceSchema);
        sb.AppendLine();

        // ---------------------------------------------------------
        // TARGET SCHEMA
        // ---------------------------------------------------------

        sb.AppendLine("TARGET DATABASE SCHEMA:");
        sb.AppendLine("=======================");
        sb.AppendLine(targetSchema);
        sb.AppendLine();

        // ---------------------------------------------------------
        // RECOMMENDED SOURCE TABLES
        // ---------------------------------------------------------

        sb.AppendLine("RECOMMENDED SOURCE ENTITIES:");
        sb.AppendLine("============================");
        sb.AppendLine(recommendedMappings);
        sb.AppendLine();

        // ---------------------------------------------------------
        // INSTRUCTIONS
        // ---------------------------------------------------------

        sb.AppendLine("Instructions:");
        sb.AppendLine();

        sb.AppendLine(
            "1. Analyze the supplied SOURCE database schema.");

        sb.AppendLine(
            "2. Analyze the supplied TARGET database schema.");

        sb.AppendLine(
            "3. SourceSchema and TargetSchema contain database schema/DDL text, not JSON schema objects.");

        sb.AppendLine(
            "4. RecommendedMappings contains ONLY source tables/entities that should be analyzed.");

        sb.AppendLine(
            "5. RecommendedMappings does NOT contain target table names.");

        sb.AppendLine(
            "6. Determine the appropriate target table for each recommended source table by comparing the SOURCE schema with the TARGET schema.");

        sb.AppendLine(
            "7. Determine the appropriate source-to-target column mappings.");

        sb.AppendLine(
            "8. Use table names, column names, data types, primary keys, foreign keys, relationships and business meaning when determining mappings.");

        sb.AppendLine(
            "9. Do not assume that the target table has the same name as the source table.");

        sb.AppendLine(
            "10. Do not invent tables or columns that do not exist in the supplied schemas.");

        sb.AppendLine(
            "11. Only create mappings for source tables specified in RecommendedMappings.");

        sb.AppendLine(
            "12. Group related mappings under the appropriate business entity.");

        sb.AppendLine(
            "13. Each entity can contain multiple mappings.");

        sb.AppendLine(
            "14. Use the recommended source entities as guidance, but independently determine the correct target table from the TARGET database schema.");

        sb.AppendLine(
            "15. If a source table cannot be reliably mapped to a target table, do not invent a mapping.");

        sb.AppendLine(
            "16. Return ONLY valid JSON.");

        sb.AppendLine(
            "17. Do not return markdown.");

        sb.AppendLine(
            "18. Do not return ```json.");

        sb.AppendLine(
            "19. Do not add explanations before or after the JSON.");

        sb.AppendLine();

        // ---------------------------------------------------------
        // OUTPUT FORMAT
        // ---------------------------------------------------------

        sb.AppendLine(
            "The response must have EXACTLY this structure:");

        sb.AppendLine();

        sb.AppendLine("[");
        sb.AppendLine("  {");
        sb.AppendLine("    \"entity\": \"User\",");
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

        // ---------------------------------------------------------
        // OUTPUT RULES
        // ---------------------------------------------------------

        sb.AppendLine("Important:");
        sb.AppendLine();

        sb.AppendLine(
            "- The root JSON element MUST be an array.");

        sb.AppendLine(
            "- Each array element MUST contain \"entity\" and \"mappings\".");

        sb.AppendLine(
            "- \"mappings\" MUST be an array.");

        sb.AppendLine(
            "- Each mapping MUST contain exactly:");

        sb.AppendLine(
            "  \"Source_TableName\",");

        sb.AppendLine(
            "  \"Target_TableName\",");

        sb.AppendLine(
            "  \"Source_ColumnName\",");

        sb.AppendLine(
            "  \"Target_ColumnName\".");

        sb.AppendLine(
            "- Target_TableName MUST be determined from the supplied TARGET database schema.");

        sb.AppendLine(
            "- Target_ColumnName MUST be determined from the supplied TARGET database schema.");

        sb.AppendLine(
            "- Do NOT return a wrapper object.");

        sb.AppendLine(
            "- Do NOT return \"entities\".");

        sb.AppendLine(
            "- Do NOT return \"Mappings\" at the root level.");

        sb.AppendLine(
            "- Return ONLY the JSON array.");

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
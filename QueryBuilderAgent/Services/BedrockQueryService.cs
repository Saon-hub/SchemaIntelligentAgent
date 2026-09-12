using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using System.Text;
using System.Text.Json;
using QueryBuilderAgent.Models;

namespace QueryBuilderAgent.Services;

public class BedrockQueryService
{
    private readonly IAmazonBedrockRuntime _bedrockRuntime;

    private static readonly string ModelId =
        Environment.GetEnvironmentVariable("BEDROCK_MODEL_ID")
        ?? "amazon.nova-lite-v1:0";

    public BedrockQueryService()
    {
        _bedrockRuntime = new AmazonBedrockRuntimeClient();
    }

    public async Task<QueryBuilderResponse> BuildQueriesAsync(
        QueryBuilderRequest request)
    {
        string prompt = BuildPrompt(request);

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
                max_new_tokens = 3000,
                temperature = 0.1
            }
        };

        string jsonBody = JsonSerializer.Serialize(requestBody);

        var bedrockRequest = new ConverseRequest
        {
            ModelId = ModelId,
            Messages = new List<Message>
            {
                new Message
                {
                    Role = ConversationRole.User,
                    Content = new List<ContentBlock>
                    {
                        new ContentBlock
                        {
                            Text = prompt
                        }
                    }
                }
            },
            InferenceConfig = new InferenceConfiguration
            {
                MaxTokens = 3000,
                Temperature = 0.1f
            }
        };

        var response = await _bedrockRuntime.ConverseAsync(bedrockRequest);

        string modelResponse =
            response.Output.Message.Content[0].Text;

        return ParseResponse(modelResponse);
    }

    private static string BuildPrompt(QueryBuilderRequest request)
    {
        string mappingJson =
            JsonSerializer.Serialize(
                request.Mapping,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        return $$$"""
You are an expert database migration SQL generation agent.

Your task is to generate SQL queries for migrating data from a source
database to a target database.

You are given exactly three inputs:

1. Entity mapping
2. Source database schema
3. Target database schema

ENTITY MAPPING:
{mappingJson}

SOURCE DATABASE SCHEMA:
{request.SourceSchema}

TARGET DATABASE SCHEMA:
{request.TargetSchema}

Generate exactly two SQL queries:

1. SOURCE EXTRACTION QUERY

This query must extract the required data from the source database.

2. TARGET INSERTION QUERY

This query must insert the extracted data into the target database.

Rules:

- Use the provided mappings.
- Source table and column names must come from the source schema/mapping.
- Target table and column names must come from the target schema/mapping.
- Do not invent tables.
- Do not invent columns.
- Include only columns that can be reliably mapped.
- Preserve the mapping between source and target columns.
- Target insertion values must use parameters such as @OrderId,
  @CustomerId, etc.
- Do not generate actual sample data.
- Do not include explanations outside the JSON.
- Do not use Markdown.
- Return ONLY valid JSON.

Return exactly this structure:
{{
  "entity": "EntityName",
  "sourceExtractionQuery": "SQL query",
  "targetInsertionQuery": "SQL query"
}}
  
""";
    }

    private static QueryBuilderResponse ParseResponse(string modelResponse)
    {
        modelResponse = modelResponse.Trim();

        if (modelResponse.StartsWith("```"))
        {
            int firstNewLine = modelResponse.IndexOf('\n');

            if (firstNewLine >= 0)
            {
                modelResponse = modelResponse[(firstNewLine + 1)..];
            }

            int lastFence = modelResponse.LastIndexOf("```");

            if (lastFence >= 0)
            {
                modelResponse = modelResponse[..lastFence];
            }
        }

        modelResponse = modelResponse.Trim();

        var result =
            JsonSerializer.Deserialize<QueryBuilderResponse>(
                modelResponse,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (result == null)
        {
            throw new InvalidOperationException(
                "Bedrock returned an empty query builder response.");
        }

        if (string.IsNullOrWhiteSpace(result.SourceExtractionQuery))
        {
            throw new InvalidOperationException(
                "Bedrock response does not contain sourceExtractionQuery.");
        }

        if (string.IsNullOrWhiteSpace(result.TargetInsertionQuery))
        {
            throw new InvalidOperationException(
                "Bedrock response does not contain targetInsertionQuery.");
        }

        return result;
    }
}
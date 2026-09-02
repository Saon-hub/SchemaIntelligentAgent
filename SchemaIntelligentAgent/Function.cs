using System.Text.Json;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace SchemaIntelligentAgent
{
    public static class Function
    {
        private static readonly IAmazonBedrockRuntime BedrockClient =
            InitializeBedrockClient();

        private static readonly string ModelId =
            Environment.GetEnvironmentVariable("BEDROCK_MODEL_ID")
            ?? "amazon.nova-lite-v1:0";


        // ============================================================
        // AWS Credentials Initialization
        // ============================================================

        private static IAmazonBedrockRuntime InitializeBedrockClient()
        {
            try
            {
                var client = new AmazonBedrockRuntimeClient(
                    RegionEndpoint.USEast1
                );

                Console.WriteLine("✓ Bedrock client initialized for us-east-1");

                return client;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Bedrock initialization failed: {ex}");
                throw;
            }
        }


        // ============================================================
        // Lambda Handler
        // ============================================================

        //private static async Task<string> Handler(
        //    string input,
        //    ILambdaContext context)
        //{
        //    context.Logger.LogInformation(
        //        "Schema Intelligence Agent started."
        //    );

        //    if (string.IsNullOrWhiteSpace(input))
        //    {
        //        throw new ArgumentException(
        //            "Schema input cannot be empty."
        //        );
        //    }

        //    // --------------------------------------------------------
        //    // Extract schema from input
        //    // --------------------------------------------------------

        //    string schema = ExtractSchema(input);

        //    if (string.IsNullOrWhiteSpace(schema))
        //    {
        //        throw new ArgumentException(
        //            "No schema was found in the input."
        //        );
        //    }

        //    context.Logger.LogInformation(
        //        $"Schema received. Length: {schema.Length} characters."
        //    );

        //    // --------------------------------------------------------
        //    // Build Bedrock prompt
        //    // --------------------------------------------------------

        //    string prompt = BuildPrompt(schema);

        //    // --------------------------------------------------------
        //    // Send schema to Bedrock
        //    // --------------------------------------------------------

        //    string bedrockResult =
        //        await AnalyzeSchemaWithBedrock(
        //            prompt,
        //            context
        //        );

        //    // --------------------------------------------------------
        //    // Return Bedrock result
        //    // --------------------------------------------------------

        //    return bedrockResult;
        //}

        private static async Task<string> Handler(SchemaRequest input,ILambdaContext context)
        {
            context.Logger.LogInformation(
                "Schema Intelligence Agent started.");

            if (input == null || string.IsNullOrWhiteSpace(input.Schema))
            {
                throw new ArgumentException(
                    "Schema input cannot be empty.");
            }

            string schema = input.Schema;

            context.Logger.LogInformation(
                $"Schema received. Length: {schema.Length} characters.");

            string prompt = BuildPrompt(schema);

            string bedrockResult =
                await AnalyzeSchemaWithBedrock(
                    prompt,
                    context);

            return bedrockResult;
        }


        // ============================================================
        // Extract Schema
        // ============================================================

        private static string ExtractSchema(string input)
        {
            input = input.Trim();

            // Raw schema text
            if (!input.StartsWith("{"))
            {
                return input;
            }

            try
            {
                using JsonDocument document =
                    JsonDocument.Parse(input);

                JsonElement root =
                    document.RootElement;

                // { "schema": "..." }

                if (root.TryGetProperty(
                    "schema",
                    out JsonElement schemaElement))
                {
                    return schemaElement.ValueKind ==
                           JsonValueKind.String
                        ? schemaElement.GetString() ?? ""
                        : schemaElement.GetRawText();
                }

                // { "content": "..." }

                if (root.TryGetProperty(
                    "content",
                    out JsonElement contentElement))
                {
                    return contentElement.ValueKind ==
                           JsonValueKind.String
                        ? contentElement.GetString() ?? ""
                        : contentElement.GetRawText();
                }

                // If the input itself is a schema JSON object,
                // send the complete object to Bedrock.

                return root.GetRawText();
            }
            catch
            {
                // If it is not valid JSON,
                // treat it as raw schema.
                return input;
            }
        }


        // ============================================================
        // Build Bedrock Prompt
        // ============================================================

        private static string BuildPrompt(string schema)
        {
            var template = """
You are a Senior Database Schema Intelligence Agent
responsible for identifying business entities and tables
that are suitable candidates for database migration.

You will receive a database schema from an unknown database
technology.

The database could be:

- Microsoft SQL Server
- Oracle
- MySQL
- PostgreSQL
- DB2
- MariaDB
- or another relational database.

You must analyze the schema itself and determine the
business entities and physical tables that are relevant
for migration.

IMPORTANT:

The purpose of this analysis is NOT to migrate the data.

The purpose is to determine:

"What business entities and tables should be considered
for migration?"

------------------------------------------------------------
ANALYSIS REQUIREMENTS
------------------------------------------------------------

Analyze the schema using all available information:

1. Table names
2. Column names
3. Column data types
4. Primary keys
5. Foreign keys
6. Relationships
7. Constraints
8. Table dependencies
9. Naming patterns
10. Table structure
11. Business semantics inferred from columns
12. Relationships between tables
13. Parent/child relationships
14. Transactional patterns
15. Reference/master data patterns

Do NOT rely only on table names.

------------------------------------------------------------
BUSINESS ENTITY IDENTIFICATION
------------------------------------------------------------

Identify logical business entities.

For example:

Customer
Order
Product
Invoice
Payment
Employee
Supplier

A business entity may consist of multiple physical tables.

For example:

Customer

could consist of:

customer
customer_address
customer_contact

If multiple tables clearly represent one business entity,
group them together.

------------------------------------------------------------
TABLE CLASSIFICATION
------------------------------------------------------------

Classify each relevant table into one of:

MASTER
TRANSACTION
REFERENCE
HISTORY
AUDIT
STAGING
SYSTEM
CONFIGURATION
LOG
UNKNOWN

------------------------------------------------------------
MIGRATION CANDIDATE
------------------------------------------------------------

For each entity determine whether it is a good candidate
for migration.

Use:

RECOMMENDED
OPTIONAL
REVIEW
EXCLUDE

RECOMMENDED means the table/entity appears to represent
important business data that should normally be migrated.

OPTIONAL means the table may be useful but is not clearly
core business data.

REVIEW means there is insufficient information to make a
confident decision.

EXCLUDE means the table appears to be system, temporary,
technical, logging, audit, staging, or otherwise unlikely
to represent business data.

------------------------------------------------------------
IMPORTANT RULES
------------------------------------------------------------

1. DO NOT invent tables.

2. Every physical table returned must exist in the
   supplied schema.

3. DO NOT invent relationships.

4. Base your reasoning only on the supplied schema.

5. Do not assume that every table should be migrated.

6. Do not automatically exclude a table simply because
   its name contains words such as "history".

7. Consider relationships when determining whether a table
   is part of an important business entity.

8. Identify dependent tables that are necessary to represent
   a complete business entity.

9. Identify tables that appear to be technical/system tables.

10. Provide an explanation for every recommendation.

11. Confidence must be between 0 and 1.

------------------------------------------------------------
OUTPUT
------------------------------------------------------------

Return ONLY valid JSON.

Do not return markdown.

Do not return ```json.

Use exactly this structure:

{
    "analysis_summary": {
        "total_tables_analyzed": 0,
        "business_entities_identified": 0,
        "recommended_entities": 0,
        "optional_entities": 0,
        "review_entities": 0,
        "excluded_entities": 0
    },

    "entities": [
        {
            "entity_name": "Customer",

            "description":
                "Represents customer master information.",

            "category": "MASTER",

            "migration_recommendation":
                "RECOMMENDED",

            "confidence": 0.97,

            "reason": [
                "Represents core customer information",
                "Contains a primary key",
                "Referenced by transaction tables"
            ],

            "tables": [
                {
                    "schema_name": "dbo",
                    "table_name": "customer",

                    "role":
                        "Primary customer table",

                    "migration_recommendation":
                        "RECOMMENDED"
                }
            ]
        }
    ]
}

------------------------------------------------------------
DATABASE SCHEMA
------------------------------------------------------------

{schema}
""";

            return template.Replace("{schema}", schema);
        }


        // ============================================================
        // Bedrock
        // ============================================================

        private static async Task<string>
            AnalyzeSchemaWithBedrock(
                string prompt,
                ILambdaContext context)
        {
            context.Logger.LogInformation(
                $"Calling Bedrock model: {ModelId}"
            );

            var request =
                new ConverseRequest
                {
                    ModelId = ModelId,

                    Messages =
                    [
                        new Message
                        {
                            Role = ConversationRole.User,

                            Content =
                            [
                                new ContentBlock
                                {
                                    Text = prompt
                                }
                            ]
                        }
                    ],

                    InferenceConfig =
                        new InferenceConfiguration
                        {
                            Temperature = 0.1f,
                            MaxTokens = 10000
                        }
                };


            var response =
                await BedrockClient.ConverseAsync(request);


            var responseText =
                response.Output.Message.Content
                    .FirstOrDefault(
                        x => !string.IsNullOrWhiteSpace(x.Text))
                    ?.Text;


            if (string.IsNullOrWhiteSpace(responseText))
            {
                throw new Exception(
                    "Bedrock returned an empty response."
                );
            }


            context.Logger.LogInformation(
                "Bedrock analysis completed."
            );


            // --------------------------------------------------------
            // Validate that Bedrock returned JSON
            // --------------------------------------------------------

            return CleanBedrockResponse(
                responseText
            );
        }


        // ============================================================
        // Clean Bedrock Response
        // ============================================================

        private static string CleanBedrockResponse(
            string response)
        {
            string result = response.Trim();

            // Remove accidental markdown fences

            if (result.StartsWith("```json"))
            {
                result = result.Substring(7);
            }
            else if (result.StartsWith("```"))
            {
                result = result.Substring(3);
            }

            if (result.EndsWith("```"))
            {
                result = result.Substring(
                    0,
                    result.Length - 3
                );
            }

            result = result.Trim();

            // Validate JSON

            using JsonDocument document =
                JsonDocument.Parse(result);

            // Return normalized JSON

            return JsonSerializer.Serialize(
                document.RootElement,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }
            );
        }


        // ============================================================
        // Lambda Runtime
        // ============================================================

        public static async Task Main()
        {
            await LambdaBootstrapBuilder
                .Create<SchemaRequest, string>(
                    Handler,
                    new DefaultLambdaJsonSerializer())
                .Build()
                .RunAsync();
        }

        // ============================================================
        // Lambda Runtime - Local test
        // ============================================================
        //public static async Task Main()
        //{
        //    Console.WriteLine("Starting local test...");

        //    try
        //    {
        //        var testSchema = """
        //{
        //  "schema": "-- Combined MySQL schema for database `store`
        //-- Source: six uploaded MySQL Workbench dumps
        //-- Structure only; no INSERT/data statements included.

        //CREATE DATABASE IF NOT EXISTS `store`;
        //USE `store`;

        //SET FOREIGN_KEY_CHECKS = 0;

        //DROP TABLE IF EXISTS `users`;
        ///*!40101 SET @saved_cs_client     = @@character_set_client */;
        ///*!50503 SET character_set_client = utf8mb4 */;
        //CREATE TABLE `users` (
        //  `user_id` int NOT NULL AUTO_INCREMENT,
        //  `username` varchar(255) NOT NULL,
        //  `password` varchar(255) NOT NULL,
        //  `email` varchar(255) NOT NULL,
        //  PRIMARY KEY (`user_id`)
        //) ENGINE=InnoDB AUTO_INCREMENT=51 DEFAULT CHARSET=latin1;
        ///*!40101 SET character_set_client = @saved_cs_client */;
        //SET @@SESSION.SQL_LOG_BIN = @MYSQLDUMP_TEMP_LOG_BIN;
        ///*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

        //DROP TABLE IF EXISTS `brands`;
        ///*!40101 SET @saved_cs_client     = @@character_set_client */;
        ///*!50503 SET character_set_client = utf8mb4 */;
        //CREATE TABLE `brands` (
        //  `brand_id` int NOT NULL AUTO_INCREMENT,
        //  `brand_name` varchar(255) NOT NULL,
        //  `brand_active` int NOT NULL DEFAULT '0',
        //  `brand_status` int NOT NULL DEFAULT '0',
        //  PRIMARY KEY (`brand_id`)
        //) ENGINE=InnoDB AUTO_INCREMENT=52 DEFAULT CHARSET=latin1;
        ///*!40101 SET character_set_client = @saved_cs_client */;
        //SET @@SESSION.SQL_LOG_BIN = @MYSQLDUMP_TEMP_LOG_BIN;
        ///*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

        //DROP TABLE IF EXISTS `categories`;
        ///*!40101 SET @saved_cs_client     = @@character_set_client */;
        ///*!50503 SET character_set_client = utf8mb4 */;
        //CREATE TABLE `categories` (
        //  `categories_id` int NOT NULL AUTO_INCREMENT,
        //  `categories_name` varchar(255) NOT NULL,
        //  `categories_active` int NOT NULL DEFAULT '0',
        //  `categories_status` int NOT NULL DEFAULT '0',
        //  PRIMARY KEY (`categories_id`)
        //) ENGINE=InnoDB AUTO_INCREMENT=51 DEFAULT CHARSET=latin1;
        ///*!40101 SET character_set_client = @saved_cs_client */;
        //SET @@SESSION.SQL_LOG_BIN = @MYSQLDUMP_TEMP_LOG_BIN;
        ///*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

        //DROP TABLE IF EXISTS `product`;
        ///*!40101 SET @saved_cs_client     = @@character_set_client */;
        ///*!50503 SET character_set_client = utf8mb4 */;
        //CREATE TABLE `product` (
        //  `product_id` int NOT NULL AUTO_INCREMENT,
        //  `product_name` varchar(255) NOT NULL,
        //  `product_image` text NOT NULL,
        //  `brand_id` int NOT NULL,
        //  `categories_id` int NOT NULL,
        //  `quantity` varchar(255) NOT NULL,
        //  `rate` varchar(255) NOT NULL,
        //  `active` int NOT NULL DEFAULT '0',
        //  `status` int NOT NULL DEFAULT '0',
        //  PRIMARY KEY (`product_id`)
        //) ENGINE=InnoDB AUTO_INCREMENT=51 DEFAULT CHARSET=latin1;
        ///*!40101 SET character_set_client = @saved_cs_client */;
        //SET @@SESSION.SQL_LOG_BIN = @MYSQLDUMP_TEMP_LOG_BIN;
        ///*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

        //DROP TABLE IF EXISTS `orders`;
        ///*!40101 SET @saved_cs_client     = @@character_set_client */;
        ///*!50503 SET character_set_client = utf8mb4 */;
        //CREATE TABLE `orders` (
        //  `order_id` int NOT NULL AUTO_INCREMENT,
        //  `order_date` date NOT NULL,
        //  `client_name` varchar(255) NOT NULL,
        //  `client_contact` varchar(255) NOT NULL,
        //  `sub_total` varchar(255) NOT NULL,
        //  `vat` varchar(255) NOT NULL,
        //  `total_amount` varchar(255) NOT NULL,
        //  `discount` varchar(255) NOT NULL,
        //  `grand_total` varchar(255) NOT NULL,
        //  `paid` varchar(255) NOT NULL,
        //  `due` varchar(255) NOT NULL,
        //  `payment_type` int NOT NULL,
        //  `payment_status` int NOT NULL,
        //  `payment_place` int NOT NULL,
        //  `gstn` varchar(255) NOT NULL,
        //  `order_status` int NOT NULL DEFAULT '0',
        //  `user_id` int NOT NULL,
        //  PRIMARY KEY (`order_id`)
        //) ENGINE=InnoDB AUTO_INCREMENT=51 DEFAULT CHARSET=latin1;
        ///*!40101 SET character_set_client = @saved_cs_client */;
        //SET @@SESSION.SQL_LOG_BIN = @MYSQLDUMP_TEMP_LOG_BIN;
        ///*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

        //DROP TABLE IF EXISTS `order_item`;
        ///*!40101 SET @saved_cs_client     = @@character_set_client */;
        ///*!50503 SET character_set_client = utf8mb4 */;
        //CREATE TABLE `order_item` (
        //  `order_item_id` int NOT NULL AUTO_INCREMENT,
        //  `order_id` int NOT NULL DEFAULT '0',
        //  `product_id` int NOT NULL DEFAULT '0',
        //  `quantity` varchar(255) NOT NULL,
        //  `rate` varchar(255) NOT NULL,
        //  `total` varchar(255) NOT NULL,
        //  `order_item_status` int NOT NULL DEFAULT '0',
        //  PRIMARY KEY (`order_item_id`)
        //) ENGINE=InnoDB AUTO_INCREMENT=51 DEFAULT CHARSET=latin1;
        ///*!40101 SET character_set_client = @saved_cs_client */;
        //SET @@SESSION.SQL_LOG_BIN = @MYSQLDUMP_TEMP_LOG_BIN;
        ///*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

        //SET FOREIGN_KEY_CHECKS = 1;
        //"
        //}
        //""";

        //        var result = await Handler(testSchema, new LocalLambdaContext());

        //        Console.WriteLine("========== RESULT ==========");
        //        Console.WriteLine(result);
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("========== ERROR ==========");
        //        Console.WriteLine(ex.ToString());
        //    }
        //}
    }
}
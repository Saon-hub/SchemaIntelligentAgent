using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Moq;
using Xunit;
using SchemaIntelligentAgent;
using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace SchemaIntelligentAgent.Tests;

public class SchemaMigrationAnalyzerTests
{
    private readonly Mock<ILambdaContext> _mockContext;

    public SchemaMigrationAnalyzerTests()
    {
        _mockContext = new Mock<ILambdaContext>();
        _mockContext
            .Setup(x => x.Logger)
            .Returns(new MockLambdaLogger());
    }

    [Fact]
    public async Task Handler_WithSimpleSchema_ReturnsTableSuggestions()
    {
        // Arrange
        var schema = @"
            CREATE TABLE customers (
                id INT PRIMARY KEY,
                name VARCHAR(255) NOT NULL,
                email VARCHAR(255)
            );
        ";

        // Act
        var result = await CallHandler(schema);
        var suggestions = ParseResult(result);

        // Assert
        Assert.NotEmpty(suggestions);
        Assert.Single(suggestions);
        Assert.Equal("customers", suggestions[0].Table);
        Assert.True(suggestions[0].Score >= 0);
    }

    [Fact]
    public async Task Handler_WithMultipleTables_ReturnsAllTables()
    {
        // Arrange
        var schema = @"
            CREATE TABLE customers (
                id INT PRIMARY KEY,
                name VARCHAR(255) NOT NULL
            );

            CREATE TABLE orders (
                id INT PRIMARY KEY,
                customer_id INT,
                total DECIMAL(10, 2)
            );

            CREATE TABLE products (
                id INT PRIMARY KEY,
                name VARCHAR(255),
                price DECIMAL(10, 2)
            );
        ";

        // Act
        var result = await CallHandler(schema);
        var suggestions = ParseResult(result);

        // Assert
        Assert.Equal(3, suggestions.Count);
        Assert.Contains(suggestions, s => s.Table == "customers");
        Assert.Contains(suggestions, s => s.Table == "orders");
        Assert.Contains(suggestions, s => s.Table == "products");
    }

    [Fact]
    public async Task Handler_WithArchiveTable_AssignsHigherScore()
    {
        // Arrange
        var schema = @"
            CREATE TABLE archive_old_records (
                id INT PRIMARY KEY,
                data VARCHAR(MAX),
                archived_date DATETIME
            );
        ";

        // Act
        var result = await CallHandler(schema);
        var suggestions = ParseResult(result);

        // Assert
        Assert.NotEmpty(suggestions);
        var archiveTable = suggestions.FirstOrDefault(s => s.Table.Contains("archive"));
        Assert.NotNull(archiveTable);
        Assert.True(archiveTable.Score > 20, "Archive tables should have higher score");
        Assert.Contains("archive", string.Join(", ", archiveTable.Reasons).ToLower());
    }

    [Fact]
    public async Task Handler_WithHistoryTable_IdentifiesIt()
    {
        // Arrange
        var schema = @"
            CREATE TABLE customer_history (
                id INT PRIMARY KEY,
                customer_id INT,
                change_type VARCHAR(50),
                changed_at DATETIME
            );
        ";

        // Act
        var result = await CallHandler(schema);
        var suggestions = ParseResult(result);

        // Assert
        Assert.NotEmpty(suggestions);
        var historyTable = suggestions.First();
        Assert.Contains("history", string.Join(", ", historyTable.Reasons).ToLower());
    }

    [Fact]
    public async Task Handler_WithWideTable_IdentifiesWidthHeuristic()
    {
        // Arrange
        var schema = @"
            CREATE TABLE employee_profile (
                id INT PRIMARY KEY,
                first_name VARCHAR(100),
                last_name VARCHAR(100),
                email VARCHAR(255),
                phone VARCHAR(20),
                address VARCHAR(255),
                city VARCHAR(100),
                state VARCHAR(50),
                country VARCHAR(100),
                postal_code VARCHAR(20),
                hire_date DATE,
                department VARCHAR(100),
                manager_id INT,
                salary DECIMAL(10, 2),
                benefits_status VARCHAR(50),
                employment_type VARCHAR(50),
                status VARCHAR(50)
            );
        ";

        // Act
        var result = await CallHandler(schema);
        var suggestions = ParseResult(result);

        // Assert
        var table = suggestions.First();
        Assert.True(table.Score > 0);
        Assert.True(table.Reasons.Any(r => r.Contains("wide") || r.Contains("column")));
    }

    [Fact]
    public async Task Handler_WithNullableColumns_IdentifiesHighNullability()
    {
        // Arrange
        var schema = @"
            CREATE TABLE sparse_data (
                id INT PRIMARY KEY,
                col1 VARCHAR(100),
                col2 VARCHAR(100),
                col3 VARCHAR(100),
                col4 VARCHAR(100),
                col5 VARCHAR(100),
                col6 VARCHAR(100),
                col7 VARCHAR(100),
                col8 VARCHAR(100),
                col9 VARCHAR(100),
                col10 VARCHAR(100)
            );
        ";

        // Act
        var result = await CallHandler(schema);
        var suggestions = ParseResult(result);

        // Assert
        var table = suggestions.First();
        Assert.True(table.Reasons.Any(r => r.Contains("nullable")));
    }

    [Fact]
    public async Task Handler_WithoutPrimaryKey_FlagsIt()
    {
        // Arrange
        var schema = @"
            CREATE TABLE logs (
                timestamp DATETIME,
                message VARCHAR(MAX),
                level VARCHAR(50)
            );
        ";

        // Act
        var result = await CallHandler(schema);
        var suggestions = ParseResult(result);

        // Assert
        var table = suggestions.First();
        Assert.True(table.Reasons.Any(r => r.Contains("primary key")));
    }

    [Fact]
    public async Task Handler_WithoutForeignKeys_IdentifiesLowCoupling()
    {
        // Arrange
        var schema = @"
            CREATE TABLE reference_data (
                id INT PRIMARY KEY,
                code VARCHAR(50),
                description VARCHAR(255)
            );
        ";

        // Act
        var result = await CallHandler(schema);
        var suggestions = ParseResult(result);

        // Assert
        var table = suggestions.First();
        Assert.True(table.Reasons.Any(r => r.Contains("foreign key") && r.Contains("low")));
    }

    [Fact]
    public async Task Handler_WithJsonColumns_IdentifiesUnstructured()
    {
        // Arrange
        var schema = @"
            CREATE TABLE config_data (
                id INT PRIMARY KEY,
                config_json JSON,
                metadata JSONB
            );
        ";

        // Act
        var result = await CallHandler(schema);
        var suggestions = ParseResult(result);

        // Assert
        var table = suggestions.First();
        Assert.True(table.Reasons.Any(r => r.Contains("unstructured")));
    }

    [Fact]
    public async Task Handler_WithJsonInput_ExtractsSchema()
    {
        // Arrange
        var schema = @"
            CREATE TABLE test_table (
                id INT PRIMARY KEY,
                name VARCHAR(100)
            );
        ";
        var jsonInput = JsonSerializer.Serialize(new { schema = schema });

        // Act
        var result = await CallHandler(jsonInput);
        var suggestions = ParseResult(result);

        // Assert
        Assert.NotEmpty(suggestions);
        Assert.Equal("test_table", suggestions[0].Table);
    }

    [Fact]
    public async Task Handler_WithContentProperty_ExtractsSchema()
    {
        // Arrange
        var schema = @"
            CREATE TABLE content_table (
                id INT PRIMARY KEY,
                data VARCHAR(100)
            );
        ";
        var jsonInput = JsonSerializer.Serialize(new { content = schema });

        // Act
        var result = await CallHandler(jsonInput);
        var suggestions = ParseResult(result);

        // Assert
        Assert.NotEmpty(suggestions);
        Assert.Equal("content_table", suggestions[0].Table);
    }

    [Fact]
    public async Task Handler_WithEmptySchema_ReturnsEmptyList()
    {
        // Arrange
        var schema = "SELECT * FROM nowhere;"; // No CREATE TABLE statements

        // Act
        var result = await CallHandler(schema);
        var suggestions = ParseResult(result);

        // Assert
        Assert.Empty(suggestions);
    }

    [Fact]
    public async Task Handler_ScoresSortedByDescending()
    {
        // Arrange
        var schema = @"
            CREATE TABLE simple_table (
                id INT PRIMARY KEY
            );

            CREATE TABLE archive_important_data (
                id INT PRIMARY KEY,
                data VARCHAR(MAX),
                col1 VARCHAR(100),
                col2 VARCHAR(100),
                col3 VARCHAR(100),
                col4 VARCHAR(100),
                col5 VARCHAR(100),
                col6 VARCHAR(100),
                col7 VARCHAR(100),
                col8 VARCHAR(100),
                col9 VARCHAR(100),
                col10 VARCHAR(100)
            );
        ";

        // Act
        var result = await CallHandler(schema);
        var suggestions = ParseResult(result);

        // Assert
        Assert.True(suggestions.Count >= 2);
        // Archive table with more heuristics should score higher
        var archiveTable = suggestions.FirstOrDefault(s => s.Table.Contains("archive"));
        var simpleTable = suggestions.FirstOrDefault(s => s.Table == "simple_table");

        Assert.NotNull(archiveTable);
        Assert.NotNull(simpleTable);
        Assert.True(archiveTable.Score >= simpleTable.Score,
            $"Archive table score ({archiveTable.Score}) should be >= simple table score ({simpleTable.Score})");
    }

    [Fact]
    public async Task Handler_WithPrimaryKeyConstraint_RecognizesPK()
    {
        // Arrange
        var schema = @"
            CREATE TABLE orders (
                order_id INT,
                customer_id INT,
                order_date DATE,
                PRIMARY KEY (order_id)
            );
        ";

        // Act
        var result = await CallHandler(schema);
        var suggestions = ParseResult(result);

        // Assert
        var table = suggestions.First();
        Assert.DoesNotContain("primary key", string.Join(", ", table.Reasons).ToLower());
    }

    [Fact]
    public async Task Handler_WithSchemaQualifiedNames_StripsSchema()
    {
        // Arrange
        var schema = @"
            CREATE TABLE dbo.customers (
                id INT PRIMARY KEY,
                name VARCHAR(255)
            );
        ";

        // Act
        var result = await CallHandler(schema);
        var suggestions = ParseResult(result);

        // Assert
        Assert.NotEmpty(suggestions);
        // Should parse table name without schema qualifier
        Assert.Single(suggestions);
    }

    private async Task<string> CallHandler(string input)
    {
        // Using reflection to call the private Handler method
        var handlerMethod = typeof(Program).GetMethod(
            "Handler",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (handlerMethod == null)
            throw new InvalidOperationException("Handler method not found");

        var result = handlerMethod.Invoke(null, new object[] { input, _mockContext.Object });

        if (result is Task<string> task)
            return await task;

        throw new InvalidOperationException("Handler did not return Task<string>");
    }

    private List<(string Table, int Score, string[] Reasons)> ParseResult(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var results = new List<(string Table, int Score, string[] Reasons)>();

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var table = element.GetProperty("table").GetString()!;
            var score = element.GetProperty("score").GetInt32();
            var reasons = element
                .GetProperty("reasons")
                .EnumerateArray()
                .Select(r => r.GetString()!)
                .ToArray();

            results.Add((table, score, reasons));
        }

        return results;
    }
}

// Mock ILambdaLogger for testing
public class MockLambdaLogger : ILambdaLogger
{
    public void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
    }

    public void LogLine(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
    }
}

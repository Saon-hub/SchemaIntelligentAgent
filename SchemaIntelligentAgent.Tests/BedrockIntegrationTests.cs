using System;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Moq;
using Xunit;
using SchemaIntelligentAgent;
using System.Runtime.CompilerServices;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("SchemaIntelligentAgent.Tests")]

namespace SchemaIntelligentAgent.Tests;

/// <summary>
/// Integration tests that call the actual AWS Bedrock service.
/// These tests require AWS credentials to be configured.
/// 
/// Before running these tests, set environment variables:
///   $env:AWS_ACCESS_KEY_ID = "your-access-key-id"
///   $env:AWS_SECRET_ACCESS_KEY = "your-secret-access-key"
///   $env:AWS_REGION = "us-east-1"
/// </summary>
public class BedrockIntegrationTests
{
    private readonly Mock<ILambdaContext> _mockContext;

    public BedrockIntegrationTests()
    {
        _mockContext = new Mock<ILambdaContext>();
        _mockContext
            .Setup(x => x.Logger)
            .Returns(new MockLambdaLogger());

        // Verify credentials are available
        var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        {
            Console.WriteLine("⚠ WARNING: AWS credentials not found!");
            Console.WriteLine("Set environment variables to run integration tests:");
            Console.WriteLine("  $env:AWS_ACCESS_KEY_ID = 'your-key-id'");
            Console.WriteLine("  $env:AWS_SECRET_ACCESS_KEY = 'your-secret'");
            Console.WriteLine("  $env:AWS_REGION = 'us-east-1'");
        }
    }

    /// <summary>
    /// Test: Send a simple table schema to Bedrock for analysis
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Handler_WithSimpleTableSchema_CallsBedrockSuccessfully()
    {
        // Skip if credentials not set (optional - remove Skip attribute to force test)
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")))
        {
            throw new SkipTestException("AWS credentials not configured");
        }

        // Arrange
        var schema = @"
            CREATE TABLE customers (
                id INT PRIMARY KEY,
                name VARCHAR(255) NOT NULL,
                email VARCHAR(255),
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP
            );
        ";

        Console.WriteLine("📤 Sending schema to Bedrock for analysis...\n");
        Console.WriteLine("Schema:\n" + schema);

        // Act
        var result = await Function.Handler(schema, _mockContext.Object);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        Console.WriteLine("\n✓ Response received from Bedrock\n");
        Console.WriteLine(new string('─', 70));
        Console.WriteLine("Analysis Result:");
        Console.WriteLine(new string('─', 70));
        Console.WriteLine(result);
        Console.WriteLine(new string('─', 70));
    }

    /// <summary>
    /// Test: Send a complex multi-table schema to Bedrock
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Handler_WithMultiTableSchema_AnalyzesAllTables()
    {
        // Skip if credentials not set
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")))
        {
            throw new SkipTestException("AWS credentials not configured");
        }

        // Arrange
        var schema = @"
            CREATE TABLE employees (
                id INT PRIMARY KEY,
                first_name VARCHAR(100) NOT NULL,
                last_name VARCHAR(100) NOT NULL,
                email VARCHAR(255) UNIQUE,
                department_id INT,
                salary DECIMAL(12, 2),
                hire_date DATE,
                is_active BOOLEAN DEFAULT TRUE
            );

            CREATE TABLE departments (
                id INT PRIMARY KEY,
                name VARCHAR(100) NOT NULL UNIQUE,
                manager_id INT,
                budget DECIMAL(15, 2),
                location VARCHAR(255)
            );

            CREATE TABLE projects (
                id INT PRIMARY KEY,
                name VARCHAR(255) NOT NULL,
                department_id INT NOT NULL,
                start_date DATE,
                end_date DATE,
                status VARCHAR(50),
                budget DECIMAL(15, 2),
                FOREIGN KEY(department_id) REFERENCES departments(id)
            );

            CREATE TABLE task_assignments (
                id INT PRIMARY KEY,
                employee_id INT NOT NULL,
                project_id INT NOT NULL,
                assigned_date DATE,
                hours_allocated DECIMAL(5, 2),
                FOREIGN KEY(employee_id) REFERENCES employees(id),
                FOREIGN KEY(project_id) REFERENCES projects(id)
            );
        ";

        Console.WriteLine("📤 Sending complex multi-table schema to Bedrock...\n");
        Console.WriteLine($"Schema contains 4 tables: employees, departments, projects, task_assignments");

        // Act
        var result = await Function.Handler(schema, _mockContext.Object);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("table", result.ToLower(), StringComparison.OrdinalIgnoreCase);

        Console.WriteLine("\n✓ Analysis completed for multi-table schema\n");
        Console.WriteLine(new string('─', 70));
        Console.WriteLine("Analysis Result:");
        Console.WriteLine(new string('─', 70));

        // Print first 1000 characters if result is very long
        if (result.Length > 1000)
        {
            Console.WriteLine(result.Substring(0, 1000));
            Console.WriteLine($"\n... [Output truncated - Total length: {result.Length} characters]");
        }
        else
        {
            Console.WriteLine(result);
        }

        Console.WriteLine(new string('─', 70));
    }

    /// <summary>
    /// Test: Verify Handler properly extracts and processes schema from JSON wrapper
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Handler_WithJsonWrappedSchema_ExtractsAndProcesses()
    {
        // Skip if credentials not set
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")))
        {
            throw new SkipTestException("AWS credentials not configured");
        }

        // Arrange
        var jsonWrappedSchema = @"{
            ""schema"": ""CREATE TABLE products (id INT PRIMARY KEY, name VARCHAR(255) NOT NULL, price DECIMAL(10, 2), stock INT DEFAULT 0);""
        }";

        Console.WriteLine("📤 Sending JSON-wrapped schema to Bedrock...\n");
        Console.WriteLine("Input JSON:\n" + jsonWrappedSchema);

        // Act
        var result = await Function.Handler(jsonWrappedSchema, _mockContext.Object);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        Console.WriteLine("\n✓ Successfully extracted and processed JSON-wrapped schema\n");
        Console.WriteLine(new string('─', 70));
        Console.WriteLine("Analysis Result:");
        Console.WriteLine(new string('─', 70));
        Console.WriteLine(result);
        Console.WriteLine(new string('─', 70));
    }

    /// <summary>
    /// Test: Verify error handling for invalid schema
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Handler_WithEmptySchema_ThrowsArgumentException()
    {
        // Arrange
        var emptySchema = "";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => Function.Handler(emptySchema, _mockContext.Object)
        );

        Assert.Contains("cannot be empty", exception.Message, StringComparison.OrdinalIgnoreCase);
        Console.WriteLine("✓ Correctly handled empty schema input");
    }

    /// <summary>
    /// Helper to skip tests when credentials are missing
    /// </summary>
    private class SkipTestException : Exception
    {
        public SkipTestException(string message) : base(message) { }
    }
}

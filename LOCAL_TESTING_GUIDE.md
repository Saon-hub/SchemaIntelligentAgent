# Local Testing Guide for SchemaIntelligentAgent

This guide explains how to test the SchemaIntelligentAgent application locally with AWS credentials.

---

## Prerequisites

- .NET 10 SDK installed
- AWS credentials configured (see `AWS_CREDENTIALS_SETUP.md`)
- Visual Studio Community 2026 or VS Code
- AWS account with Bedrock access enabled

---

## Testing Approaches

### 1. **Unit Tests (No AWS Required)** ✅ EASIEST
### 2. **Integration Tests (Requires AWS Credentials)** 
### 3. **Local Lambda Testing (Requires AWS SAM CLI)**
### 4. **Direct Manual Testing via Console**

---

## Approach 1: Run Unit Tests (Recommended First Step)

The existing tests use mocked Lambda context and don't require AWS credentials.

### Run All Tests
```powershell
# Navigate to workspace
cd C:\Users\Saon Mukherjee\source\repos\SchemaIntelligentAgent\

# Run all tests
dotnet test

# Or with verbose output
dotnet test --verbosity detailed

# Run with test output
dotnet test --logger "console;verbosity=detailed"
```

### Run Specific Test
```powershell
# Run a specific test class
dotnet test --filter "ClassName=SchemaMigrationAnalyzerTests"

# Run a specific test method
dotnet test --filter "Name~Handler_WithSimpleSchema_ReturnsTableSuggestions"
```

### Expected Output
```
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

[xUnit.net 00:00:00.XX] SchemaMigrationAnalyzerTests.Handler_WithSimpleSchema_ReturnsTableSuggestions [PASSED]
```

---

## Approach 2: Integration Tests (With AWS Credentials)

These tests call actual AWS Bedrock service.

### Step 1: Set AWS Credentials

**PowerShell:**
```powershell
# Set credentials as environment variables
$env:AWS_ACCESS_KEY_ID = "your-access-key-id"
$env:AWS_SECRET_ACCESS_KEY = "your-secret-access-key"
$env:AWS_REGION = "us-east-1"
$env:BEDROCK_MODEL_ID = "amazon.nova-lite-v1:0"

# Verify they're set
Write-Host "Region: $env:AWS_REGION"
Write-Host "Access Key (First 10): $($env:AWS_ACCESS_KEY_ID.Substring(0, 10))***"
```

### Step 2: Create Integration Test File

Create `SchemaIntelligentAgent.Tests/IntegrationTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Moq;
using Xunit;
using SchemaIntelligentAgent;

namespace SchemaIntelligentAgent.Tests;

public class IntegrationTests
{
	private readonly Mock<ILambdaContext> _mockContext;

	public IntegrationTests()
	{
		_mockContext = new Mock<ILambdaContext>();
		_mockContext
			.Setup(x => x.Logger)
			.Returns(new MockLambdaLogger());
	}

	[Fact(Skip = "Integration Test - Requires AWS Credentials")]
	public async Task Handler_CallsBedrock_WithValidSchema()
	{
		// Arrange
		var schema = @"
			CREATE TABLE employees (
				id INT PRIMARY KEY,
				name VARCHAR(255) NOT NULL,
				department VARCHAR(100),
				salary DECIMAL(10, 2)
			);
		";

		// Act
		var result = await Function.Handler(schema, _mockContext.Object);

		// Assert
		Assert.NotNull(result);
		Assert.NotEmpty(result);
		Console.WriteLine($"Bedrock Response:\n{result}");
	}

	[Fact(Skip = "Integration Test - Requires AWS Credentials")]
	public async Task Handler_WithComplexSchema_AnalyzesCorrectly()
	{
		// Arrange
		var schema = @"
			CREATE TABLE orders (
				id INT PRIMARY KEY,
				customer_id INT NOT NULL,
				order_date DATE NOT NULL,
				total_amount DECIMAL(12, 2),
				status VARCHAR(50)
			);

			CREATE TABLE order_items (
				id INT PRIMARY KEY,
				order_id INT NOT NULL,
				product_id INT NOT NULL,
				quantity INT,
				unit_price DECIMAL(10, 2)
			);
		";

		// Act
		var result = await Function.Handler(schema, _mockContext.Object);

		// Assert
		Assert.NotNull(result);
		Assert.Contains("table", result.ToLower());
		Console.WriteLine($"Analysis Result:\n{result}");
	}
}
```

### Step 3: Enable Integration Tests

To run integration tests (removes the Skip attribute):

```powershell
# Set credentials first
$env:AWS_ACCESS_KEY_ID = "your-access-key-id"
$env:AWS_SECRET_ACCESS_KEY = "your-secret-access-key"
$env:AWS_REGION = "us-east-1"

# Run tests including integration tests
dotnet test --filter "ClassName=IntegrationTests"
```

---

## Approach 3: Manual Interactive Testing

### Option A: Create a Console Test Program

Create `TestProgram.cs` in the main project root:

```csharp
using System;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Moq;
using SchemaIntelligentAgent;

class TestProgram
{
	static async Task Main(string[] args)
	{
		Console.WriteLine("┌─────────────────────────────────────────┐");
		Console.WriteLine("│ SchemaIntelligentAgent - Local Tester   │");
		Console.WriteLine("└─────────────────────────────────────────┘\n");

		// Check credentials
		var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
		var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
		var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";

		if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
		{
			Console.WriteLine("❌ ERROR: AWS credentials not set!");
			Console.WriteLine("\nSet credentials using:");
			Console.WriteLine("  $env:AWS_ACCESS_KEY_ID = 'your-key-id'");
			Console.WriteLine("  $env:AWS_SECRET_ACCESS_KEY = 'your-secret-key'");
			Console.WriteLine("  $env:AWS_REGION = 'us-east-1'");
			return;
		}

		Console.WriteLine("✓ AWS Credentials found");
		Console.WriteLine($"✓ Region: {region}");
		Console.WriteLine($"✓ Access Key (first 10): {accessKey.Substring(0, Math.Min(10, accessKey.Length))}***\n");

		// Create mock context
		var mockContext = new Mock<ILambdaContext>();
		mockContext
			.Setup(x => x.Logger)
			.Returns(new MockLambdaLogger());

		// Test schemas
		var testSchemas = new[]
		{
			new { Name = "Simple Table", Schema = @"
				CREATE TABLE customers (
					id INT PRIMARY KEY,
					name VARCHAR(255) NOT NULL,
					email VARCHAR(255)
				);
			" },
			new { Name = "Multiple Tables", Schema = @"
				CREATE TABLE users (
					id INT PRIMARY KEY,
					username VARCHAR(100) NOT NULL UNIQUE,
					email VARCHAR(255)
				);

				CREATE TABLE posts (
					id INT PRIMARY KEY,
					user_id INT NOT NULL,
					title VARCHAR(255),
					content TEXT,
					FOREIGN KEY(user_id) REFERENCES users(id)
				);
			" }
		};

		// Run tests
		foreach (var test in testSchemas)
		{
			Console.WriteLine($"\n{'='} Testing: {test.Name} {'='}\n");
			try
			{
				var result = await Function.Handler(test.Schema, mockContext.Object);
				Console.WriteLine("Response from Bedrock:");
				Console.WriteLine(new string('─', 60));
				Console.WriteLine(result.Length > 500 
					? result.Substring(0, 500) + "...\n[TRUNCATED]" 
					: result);
				Console.WriteLine(new string('─', 60));
			}
			catch (Exception ex)
			{
				Console.WriteLine($"❌ Error: {ex.Message}");
				Console.WriteLine($"Stack Trace: {ex.StackTrace}");
			}
		}

		Console.WriteLine("\n\nTest completed!");
	}
}
```

### Run Manual Test

```powershell
# Set credentials
$env:AWS_ACCESS_KEY_ID = "your-access-key-id"
$env:AWS_SECRET_ACCESS_KEY = "your-secret-access-key"
$env:AWS_REGION = "us-east-1"

# Run the test
dotnet run --project TestProgram.csproj
```

---

## Approach 4: Debug Mode in Visual Studio

### Step 1: Set Credentials in Visual Studio Debug Profile

Edit `Properties/launchSettings.json`:

```json
{
  "profiles": {
	"SchemaIntelligentAgent": {
	  "commandName": "Project",
	  "environmentVariables": {
		"AWS_ACCESS_KEY_ID": "your-access-key-id",
		"AWS_SECRET_ACCESS_KEY": "your-secret-access-key",
		"AWS_REGION": "us-east-1",
		"BEDROCK_MODEL_ID": "amazon.nova-lite-v1:0"
	  }
	}
  }
}
```

### Step 2: Debug Tests in Visual Studio

1. Open Test Explorer: `Test` → `Test Explorer`
2. Find test in the list
3. Right-click → `Debug Selected Tests`

---

## Quick Testing Command Cheatsheet

```powershell
# ========== SETUP ==========
# Set credentials (do this first!)
$env:AWS_ACCESS_KEY_ID = "your-key-id"
$env:AWS_SECRET_ACCESS_KEY = "your-secret"
$env:AWS_REGION = "us-east-1"

# ========== UNIT TESTS (No AWS needed) ==========
# Run all unit tests
dotnet test

# Run specific test class
dotnet test --filter "ClassName=SchemaMigrationAnalyzerTests"

# Run with verbose output
dotnet test -v detailed

# ========== BUILD & RUN ==========
# Build the project
dotnet build

# Run the application
dotnet run

# Restore packages (if needed)
dotnet restore

# ========== PUBLISH ==========
# Publish for Lambda deployment
dotnet publish -c Release -o ./publish
```

---

## Debugging Tips

### View Actual Error Messages

```powershell
# Run tests with full exception details
dotnet test --logger "console;verbosity=detailed" -- RunConfiguration.TestSessionTimeout=60000
```

### Check Credentials at Runtime

Add this to test or application:

```csharp
// Verify credentials are loaded
var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
var region = Environment.GetEnvironmentVariable("AWS_REGION");

Console.WriteLine($"Access Key Set: {!string.IsNullOrEmpty(accessKey)}");
Console.WriteLine($"Secret Key Set: {!string.IsNullOrEmpty(secretKey)}");
Console.WriteLine($"Region: {region ?? "not set (will use default)"}");
```

### Test AWS Connectivity

```powershell
# Install AWS CLI if not already installed
# Then run:
aws sts get-caller-identity

# You should see output like:
# {
#     "UserId": "XXXXXXXXXX",
#     "Account": "123456789012",
#     "Arn": "arn:aws:iam::123456789012:user/your-username"
# }
```

---

## Common Issues & Solutions

### ❌ "Unable to find credentials"
**Solution:**
```powershell
# Verify credentials are set
Write-Host "Access Key: $env:AWS_ACCESS_KEY_ID"
Write-Host "Region: $env:AWS_REGION"

# Re-set them if needed
$env:AWS_ACCESS_KEY_ID = "your-key-id"
$env:AWS_SECRET_ACCESS_KEY = "your-secret"
```

### ❌ "User is not authorized to perform: bedrock:InvokeModel"
**Solution:**
- Your IAM user needs Bedrock permissions
- Attach this policy to your IAM user:
```json
{
  "Version": "2012-10-17",
  "Statement": [
	{
	  "Effect": "Allow",
	  "Action": "bedrock:InvokeModel",
	  "Resource": "arn:aws:bedrock:*:*:foundation-model/*"
	}
  ]
}
```

### ❌ "Region not found: [region-name]"
**Solution:**
```powershell
# Use a valid AWS region
$env:AWS_REGION = "us-east-1"  # or us-west-2, eu-west-1, etc.
```

### ❌ "Model not found: amazon.nova-lite-v1:0"
**Solution:**
- Model may not be available in your region
- Check Bedrock console for available models in your region
- Update BEDROCK_MODEL_ID environment variable

---

## Testing Workflow Summary

```
1. RUN UNIT TESTS FIRST
   └─→ dotnet test
	   (No AWS credentials needed, fast feedback)

2. SET AWS CREDENTIALS
   └─→ $env:AWS_ACCESS_KEY_ID = "..."
	   $env:AWS_SECRET_ACCESS_KEY = "..."
	   $env:AWS_REGION = "us-east-1"

3. RUN INTEGRATION TESTS
   └─→ dotnet test --filter "ClassName=IntegrationTests"
	   (Calls actual Bedrock service)

4. DEBUG IN VS IF ISSUES ARISE
   └─→ Set breakpoints, right-click test, "Debug Selected Tests"

5. MANUAL TESTING WITH CONSOLE APP
   └─→ dotnet run
	   (Interactive testing with various schemas)
```

---

## Next Steps

1. ✅ Run unit tests: `dotnet test`
2. ✅ Set AWS credentials (see `AWS_CREDENTIALS_SETUP.md`)
3. ✅ Run integration tests: `dotnet test --filter "ClassName=IntegrationTests"`
4. ✅ Debug in Visual Studio if needed
5. ✅ Review results and validate behavior

For more information:
- [xUnit.net Documentation](https://xunit.net/)
- [AWS SDK for .NET](https://docs.aws.amazon.com/sdk-for-net/)
- [Bedrock API Reference](https://docs.aws.amazon.com/bedrock/latest/userguide/what-is-bedrock.html)

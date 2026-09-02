# AWS Credentials Setup Guide

This document explains how to configure AWS credentials for the SchemaIntelligentAgent application.

## Overview

The application uses AWS Bedrock and automatically detects credentials through the AWS SDK credential chain in this order:

1. **Environment Variables** (highest priority for local development)
2. **AWS Credentials File** (~/.aws/credentials)
3. **AWS Config File** (~/.aws/config)
4. **IAM Roles** (EC2, ECS, Lambda - automatic when running in AWS)
5. **Lambda Execution Role** (automatic when running as Lambda function)

---

## Option 1: Using Environment Variables (Recommended for Development)

### PowerShell
```powershell
# Set AWS credentials as environment variables
$env:AWS_ACCESS_KEY_ID = "your-access-key-id"
$env:AWS_SECRET_ACCESS_KEY = "your-secret-access-key"
$env:AWS_REGION = "us-east-1"

# Verify they are set
Write-Host "AWS_ACCESS_KEY_ID: $env:AWS_ACCESS_KEY_ID"
Write-Host "AWS_REGION: $env:AWS_REGION"

# Run your application
dotnet run
```

### Command Prompt
```cmd
set AWS_ACCESS_KEY_ID=your-access-key-id
set AWS_SECRET_ACCESS_KEY=your-secret-access-key
set AWS_REGION=us-east-1

dotnet run
```

### Bash/Linux/macOS
```bash
export AWS_ACCESS_KEY_ID="your-access-key-id"
export AWS_SECRET_ACCESS_KEY="your-secret-access-key"
export AWS_REGION="us-east-1"

dotnet run
```

---

## Option 2: Using AWS Credentials File

### Step 1: Create or Edit `~/.aws/credentials`

**Windows**: `C:\Users\YourUsername\.aws\credentials`
**Linux/macOS**: `~/.aws/credentials`

```
[default]
aws_access_key_id = your-access-key-id
aws_secret_access_key = your-secret-access-key

[profile-name]
aws_access_key_id = another-access-key-id
aws_secret_access_key = another-secret-access-key
```

### Step 2: Create or Edit `~/.aws/config`

**Windows**: `C:\Users\YourUsername\.aws\config`
**Linux/macOS**: `~/.aws/config`

```
[default]
region = us-east-1
output = json

[profile profile-name]
region = us-west-2
output = json
```

### Step 3: Set Environment Variable (Optional)

```powershell
$env:AWS_PROFILE = "default"  # or "profile-name"

dotnet run
```

---

## Option 3: Using AWS CLI to Configure Credentials

If you have the AWS CLI installed:

```bash
aws configure
```

This will prompt you to enter:
- AWS Access Key ID
- AWS Secret Access Key
- Default region
- Default output format

---

## Specific Configuration for This Project

### Required AWS Credentials

Your AWS credentials must have permissions for:
- **bedrock:InvokeModel** - To call Bedrock models
- **bedrock:InvokeModelWithResponseStream** - For streaming responses (if used)

### Example IAM Policy

```json
{
  "Version": "2012-10-17",
  "Statement": [
	{
	  "Effect": "Allow",
	  "Action": [
		"bedrock:InvokeModel",
		"bedrock:InvokeModelWithResponseStream"
	  ],
	  "Resource": "arn:aws:bedrock:*:*:foundation-model/*"
	}
  ]
}
```

### Environment Variables Used by This App

```
AWS_ACCESS_KEY_ID          - Your AWS access key
AWS_SECRET_ACCESS_KEY      - Your AWS secret access key
AWS_REGION                 - AWS region (default: us-east-1)
AWS_DEFAULT_REGION         - Fallback region setting
AWS_PROFILE                - AWS credentials profile name
BEDROCK_MODEL_ID           - Bedrock model to use (default: amazon.nova-lite-v1:0)
```

---

## Verification

To verify your credentials are working:

```powershell
# PowerShell - Check environment variables
$env:AWS_ACCESS_KEY_ID
$env:AWS_SECRET_ACCESS_KEY
$env:AWS_REGION

# Or use AWS CLI to verify setup
aws sts get-caller-identity
```

If using AWS CLI returns your account ID and user ARN, your credentials are properly configured.

---

## For Lambda Deployment

When deploying to AWS Lambda:
1. **No explicit credentials needed** - Lambda automatically uses the IAM execution role
2. **Ensure the Lambda role has Bedrock permissions** - Attach an IAM policy with `bedrock:InvokeModel` permissions
3. **Set environment variables in Lambda configuration**:
   - `AWS_REGION` (optional, Lambda sets a default)
   - `BEDROCK_MODEL_ID` (if you want to override the default model)

---

## Security Best Practices

⚠️ **IMPORTANT:**
- Never commit AWS credentials to version control
- Never share your access keys or secret access keys
- Use AWS IAM roles when possible instead of long-term credentials
- Rotate access keys regularly
- Use separate profiles for different environments (dev, staging, production)
- Store credentials in environment-specific `.env` files that are gitignored

### Create a `.env` file (Add to `.gitignore`)

```
# .env (Add this to .gitignore)
AWS_ACCESS_KEY_ID=your-access-key-id
AWS_SECRET_ACCESS_KEY=your-secret-access-key
AWS_REGION=us-east-1
BEDROCK_MODEL_ID=amazon.nova-lite-v1:0
```

Then load it in your PowerShell profile or before running:

```powershell
# Load .env file
Get-Content .env | ForEach-Object {
	$parts = $_ -split '='
	if ($parts.Length -eq 2) {
		[Environment]::SetEnvironmentVariable($parts[0], $parts[1], "Process")
	}
}

dotnet run
```

---

## Troubleshooting

### Error: "Unable to find credentials"
- Ensure AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY are set
- Check the credentials file at `~/.aws/credentials` exists
- Verify credentials have not expired or been revoked

### Error: "User is not authorized to perform: bedrock:InvokeModel"
- Check IAM policy attached to your user/role includes Bedrock permissions
- Ensure policy resource matches your region and model

### Error: "Region not found"
- Verify AWS_REGION is set to a valid AWS region (e.g., us-east-1, us-west-2)
- Check if Bedrock is available in your region

### Error: "Model not found"
- Verify BEDROCK_MODEL_ID is valid and available
- Check that the model ID matches your selected region

---

## Next Steps

1. Choose one of the credential configuration methods above
2. Set up your credentials according to your preferred method
3. Run the application: `dotnet run`
4. The application will log: "✓ Bedrock client initialized for region: [region-name]"

For more information, visit:
- [AWS SDK for .NET Documentation](https://docs.aws.amazon.com/sdk-for-net/)
- [Credential Chain Documentation](https://docs.aws.amazon.com/general/latest/gr/aws-sec-cred-types.html)

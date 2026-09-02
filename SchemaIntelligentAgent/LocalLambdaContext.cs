using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaIntelligentAgent
{
    internal class LocalLambdaContext : ILambdaContext
    {
        public ILambdaLogger Logger { get; } = new LocalLambdaLogger();

        public string FunctionName => "LocalTest";
        public string FunctionVersion => "1";
        public string InvokedFunctionArn => "local";
        public string AwsRequestId => Guid.NewGuid().ToString();
        public IClientContext? ClientContext => null;
        public ICognitoIdentity? Identity => null;
        public int MemoryLimitInMB => 512;
        public TimeSpan RemainingTime => TimeSpan.FromMinutes(5);
        public string LogGroupName => "LocalTest";
        public string LogStreamName => "LocalTest";
    }
    public class SchemaRequest
    {
        public string Schema { get; set; } = string.Empty;
    }

    internal class LocalLambdaLogger : ILambdaLogger
    {
        public void Log(string message) => Console.WriteLine(message);
        public void LogLine(string message) => Console.WriteLine(message);
        public void LogLine(string format, params object[] args)
            => Console.WriteLine(format, args);
    }
}

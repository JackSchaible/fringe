using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using DotNetEnv;
using Fringe.Data;
using FringeTransferMatrix;
using FringeTransferMatrix.Services;
using Microsoft.Extensions.Configuration;

// Load .env — check CWD first (dotnet run from Fringe.TransferMatrix/),
// then the Fringe.TransferMatrix/ subdirectory (dotnet run --project from repo root).
string envPath = File.Exists(".env") ? ".env" : Path.Combine("Fringe.TransferMatrix", ".env");
bool envLoaded = File.Exists(envPath);
if (envLoaded)
{
    _ = Env.Load(envPath);
}
else
{
    Console.WriteLine($"⚠️  No .env file found (checked: {envPath}). Using environment variables only.");
}

IConfigurationRoot config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appSettings.json", optional: true, reloadOnChange: true)
    .Build();

string tableName = config["DynamoTableName"]
    ?? Environment.GetEnvironmentVariable("DYNAMO_TABLE_NAME")
    ?? "";

if (string.IsNullOrWhiteSpace(tableName))
{
    TransferMatrixLogger.LogError("❌ DYNAMO_TABLE_NAME is not set. Add it to Fringe.TransferMatrix/.env or set the environment variable.");
    return;
}

Environment.SetEnvironmentVariable("DYNAMO_TABLE_NAME", tableName);

string? dynamoEndpoint = Environment.GetEnvironmentVariable("DYNAMO_ENDPOINT");

Console.WriteLine($"📋 Config: table={tableName}, endpoint={dynamoEndpoint ?? "(AWS default)"}");

using AmazonDynamoDBClient dynamoClient = !string.IsNullOrEmpty(dynamoEndpoint)
    ? new AmazonDynamoDBClient(
        new Amazon.Runtime.BasicAWSCredentials("local", "local"),
        new AmazonDynamoDBConfig { ServiceURL = dynamoEndpoint })
    : new AmazonDynamoDBClient();
IDynamoDBContext dynamoContext = new DynamoDBContextBuilder()
    .WithDynamoDBClient(() => dynamoClient)
    .Build();
FringeRepository repository = new(dynamoContext);

string? openRouteServiceApiKey = config["OpenRouteServiceApiKey"]
    ?? Environment.GetEnvironmentVariable("OPENROUTESERVICE_API_KEY");
IMatrixProvider? matrixProvider = string.IsNullOrWhiteSpace(openRouteServiceApiKey)
    ? null
    : new OpenRouteServiceMatrixProvider(openRouteServiceApiKey);
if (matrixProvider == null)
{
    TransferMatrixLogger.Log("⚠️  OPENROUTESERVICE_API_KEY is not set. Transfer matrix generation will be skipped.");
}

await TransferMatrixRunner.RunAsync(repository, matrixProvider).ConfigureAwait(false);

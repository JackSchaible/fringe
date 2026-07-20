using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using DotNetEnv;
using Fringe.Data;
using FringeScraper;
using FringeScraper.Services;
using Microsoft.Extensions.Configuration;

// Load .env — check CWD first (dotnet run from Fringe.Scraper/),
// then the Fringe.Scraper/ subdirectory (dotnet run --project from repo root).
string envPath = File.Exists(".env") ? ".env" : Path.Combine("Fringe.Scraper", ".env");
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
    ScraperLogger.LogError("❌ DYNAMO_TABLE_NAME is not set. Add it to Fringe.Scraper/.env or set the environment variable.");
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
IGeocodingProvider? geocodingProvider = string.IsNullOrWhiteSpace(openRouteServiceApiKey)
    ? null
    : new OpenRouteServiceGeocodingProvider(openRouteServiceApiKey);
if (geocodingProvider == null)
{
    ScraperLogger.Log("⚠️  OPENROUTESERVICE_API_KEY is not set. Venue coordinate enrichment will be skipped.");
}

await ScraperRunner.RunAsync(repository, new Fetcher(), geocodingProvider).ConfigureAwait(false);

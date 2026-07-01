using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Fringe.Data;
using FringeScraper;
using Microsoft.Extensions.Configuration;

IConfigurationRoot config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appSettings.json", optional: true, reloadOnChange: true)
    .Build();

string tableName = config["DynamoTableName"]
    ?? Environment.GetEnvironmentVariable("DYNAMO_TABLE_NAME")
    ?? "fringe";

Environment.SetEnvironmentVariable("DYNAMO_TABLE_NAME", tableName);

AmazonDynamoDBClient dynamoClient = new();
IDynamoDBContext dynamoContext = new DynamoDBContextBuilder()
    .WithDynamoDBClient(() => dynamoClient)
    .Build();
FringeRepository repository = new(dynamoContext);

await ScraperRunner.RunAsync(repository);

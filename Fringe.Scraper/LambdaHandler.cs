using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Core;
using Fringe.Data;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace FringeScraper;

public class LambdaHandler
{
    private readonly FringeRepository _repository;

    public LambdaHandler()
    {
        var client = new AmazonDynamoDBClient();
        var context = new DynamoDBContextBuilder()
            .WithDynamoDBClient(() => client)
            .Build();
        _repository = new FringeRepository(context);
    }

    public async Task FunctionHandler(ILambdaContext context)
    {
        string? tableName = Environment.GetEnvironmentVariable("DYNAMO_TABLE_NAME");
        if (string.IsNullOrWhiteSpace(tableName))
        {
            context.Logger.LogError("❌ DYNAMO_TABLE_NAME is not set. Set it in the Lambda environment configuration.");
            return;
        }

        context.Logger.LogInformation($"Fringe Scraper Lambda invoked. Table: {tableName}");
        await ScraperRunner.RunAsync(_repository);
    }
}

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Core;
using Fringe.Data;
using FringeTransferMatrix.Services;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace FringeTransferMatrix;

/// <summary>Lambda entry point that invokes the transfer-matrix generation pipeline on each EventBridge trigger.</summary>
internal sealed class LambdaHandler : IDisposable
{
    private readonly AmazonDynamoDBClient dynamoClient;
    private readonly FringeRepository repository;

    /// <summary>Initialises a new instance of the <see cref="LambdaHandler"/> class.</summary>
    public LambdaHandler()
    {
        dynamoClient = new AmazonDynamoDBClient();
        IDynamoDBContext context = new DynamoDBContextBuilder()
            .WithDynamoDBClient(() => dynamoClient)
            .Build();
        repository = new FringeRepository(context);
    }

    /// <summary>Releases the DynamoDB client.</summary>
    public void Dispose()
    {
        dynamoClient.Dispose();
    }

    /// <summary>Runs the transfer-matrix pipeline when invoked by EventBridge.</summary>
    public async Task FunctionHandler(ILambdaContext context)
    {
        string? tableName = Environment.GetEnvironmentVariable("DYNAMO_TABLE_NAME");
        if (string.IsNullOrWhiteSpace(tableName))
        {
            context.Logger.LogError(TransferMatrixLogger.AsString("❌ DYNAMO_TABLE_NAME is not set. Set it in the Lambda environment configuration."));
            return;
        }

        string? openRouteServiceApiKey = Environment.GetEnvironmentVariable("OPENROUTESERVICE_API_KEY");
        IMatrixProvider? matrixProvider = string.IsNullOrWhiteSpace(openRouteServiceApiKey)
            ? null
            : new OpenRouteServiceMatrixProvider(openRouteServiceApiKey);

        context.Logger.LogInformation($"Fringe Transfer Matrix Lambda invoked. Table: {tableName}");
        await TransferMatrixRunner.RunAsync(repository, matrixProvider).ConfigureAwait(false);
    }
}

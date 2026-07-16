using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Fringe.API;
using Fringe.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Controllers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApplicationPartManager(static apm =>
    {
        for (int i = apm.FeatureProviders.Count - 1; i >= 0; i--)
        {
            if (apm.FeatureProviders[i] is ControllerFeatureProvider)
            {
                apm.FeatureProviders.RemoveAt(i);
            }
        }
        apm.FeatureProviders.Add(new InternalControllerFeatureProvider());
    });
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAmazonCognitoIdentityProvider, AmazonCognitoIdentityProviderClient>();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

string? dynamoEndpoint = Environment.GetEnvironmentVariable("DYNAMO_ENDPOINT");
_ = !string.IsNullOrEmpty(dynamoEndpoint)
    ? builder.Services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(
        new Amazon.Runtime.BasicAWSCredentials("local", "local"),
        new AmazonDynamoDBConfig { ServiceURL = dynamoEndpoint }
    ))
    : builder.Services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
builder.Services.AddSingleton<IDynamoDBContext>(sp =>
    new DynamoDBContextBuilder()
        .WithDynamoDBClient(() => sp.GetRequiredService<IAmazonDynamoDB>())
        .Build());
builder.Services.AddScoped<FringeRepository>();

// Auth: Cognito JWT in production, dev stub locally (pass X-Dev-User-Id header)
string? userPoolId = Environment.GetEnvironmentVariable("COGNITO_USER_POOL_ID");
string region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";

if (!string.IsNullOrEmpty(userPoolId))
{
    string? cognitoClientId = Environment.GetEnvironmentVariable("COGNITO_CLIENT_ID");
    _ = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";
            if (!string.IsNullOrEmpty(cognitoClientId))
            {
                options.Audience = cognitoClientId;
            }
        });
}
else
{
    _ = builder.Services.AddAuthentication("Dev")
        .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>("Dev", null);
}
builder.Services.AddAuthorization();

string[] allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")?.Split(';') ?? [];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

WebApplication app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => "Fringe API");

app.Run();

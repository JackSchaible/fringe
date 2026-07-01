using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Fringe.API;
using Fringe.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAmazonCognitoIdentityProvider, AmazonCognitoIdentityProviderClient>();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

var dynamoEndpoint = Environment.GetEnvironmentVariable("DYNAMO_ENDPOINT");
if (!string.IsNullOrEmpty(dynamoEndpoint))
{
    builder.Services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(
        new Amazon.Runtime.BasicAWSCredentials("local", "local"),
        new AmazonDynamoDBConfig { ServiceURL = dynamoEndpoint }
    ));
}
else
{
    builder.Services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
}
builder.Services.AddSingleton<IDynamoDBContext>(sp =>
    new DynamoDBContextBuilder()
        .WithDynamoDBClient(() => sp.GetRequiredService<IAmazonDynamoDB>())
        .Build());
builder.Services.AddScoped<FringeRepository>();

// Auth: Cognito JWT in production, dev stub locally (pass X-Dev-User-Id header)
var userPoolId = Environment.GetEnvironmentVariable("COGNITO_USER_POOL_ID");
var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";

if (!string.IsNullOrEmpty(userPoolId))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";
            options.TokenValidationParameters.ValidateAudience = false;
        });
}
else
{
    builder.Services.AddAuthentication("Dev")
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

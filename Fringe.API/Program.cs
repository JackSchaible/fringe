using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Fringe.API;
using Fringe.API.Services;
using Fringe.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.IdentityModel.Tokens.Jwt;

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
builder.Services.AddSingleton(new TransferPolicyOptions());
builder.Services.AddScoped<IVenueTransferTimeProvider, VenueTransferTimeProvider>();
builder.Services.AddScoped<IScheduleBuilder, ScheduleBuilder>();

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

            // JwtBearerOptions.MapInboundClaims defaults to true, which renames
            // short JWT claim names to legacy XML-SOAP claim type URIs (e.g.
            // "sub" becomes ClaimTypes.NameIdentifier). GetUserId() below reads
            // User.FindFirst("sub") directly, so without this every request
            // resolved to an empty user id — every real user silently shared
            // one backend identity. https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.jwtbearer.jwtbeareroptions.mapinboundclaims
            options.MapInboundClaims = false;

            // Cognito access tokens (what the frontend sends) carry the app
            // client in a `client_id` claim, not the standard `aud` claim that
            // options.Audience checks against — so validate that claim directly
            // instead of leaving audience validation off entirely.
            options.TokenValidationParameters.AudienceValidator = (_, securityToken, _) =>
                string.IsNullOrEmpty(cognitoClientId) ||
                (securityToken is JwtSecurityToken jwt &&
                    jwt.Claims.Any(claim => claim.Type == "client_id" && claim.Value == cognitoClientId));
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

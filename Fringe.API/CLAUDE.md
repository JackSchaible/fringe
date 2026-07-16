# Fringe.API

ASP.NET Core Web API hosted on AWS Lambda via `Amazon.Lambda.AspNetCoreServer.Hosting`.

## Lambda hosting

`AddAWSLambdaHosting(LambdaEventSource.RestApi)` in `Program.cs` switches Kestrel out for the Lambda event bridge when running in AWS. Locally it behaves as a normal ASP.NET Core app.

Publish path expected by CDK: `Fringe.API/publish/`

```bash
dotnet publish -c Release -o publish
```

## DI setup

- `IAmazonDynamoDB` → singleton `AmazonDynamoDBClient` (credentials from Lambda execution role in AWS, from local profile/env locally)
- `IDynamoDBContext` → singleton built via `DynamoDBContextBuilder`
- `FringeRepository` → scoped

## Adding endpoints

Add controllers to `Controllers/`. The `FringeRepository` is available for injection — it covers shows, showtimes, votes. See `Fringe.Data/CLAUDE.md` for extending the data layer.

## Environment variables

| Variable            | Purpose                            |
| ------------------- | ---------------------------------- |
| `DYNAMO_TABLE_NAME` | DynamoDB table (default: `fringe`) |
| `ALLOWED_ORIGINS`   | Semicolon-separated CORS origins   |

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
- `TransferPolicyOptions` → singleton, constructed with its class defaults (no env var overrides yet — see `Services/TransferPolicyOptions.cs` for the tunable knobs)
- `IVenueTransferTimeProvider` → scoped `VenueTransferTimeProvider`

## Adding endpoints

Add controllers to `Controllers/`. The `FringeRepository` is available for injection — it covers shows, showtimes, votes. See `Fringe.Data/CLAUDE.md` for extending the data layer.

## Services/

Application-layer logic that's more than a repository call but doesn't belong in `Fringe.Data` (no DynamoDB access of its own, or ASP.NET-specific). Currently just `IVenueTransferTimeProvider`/`VenueTransferTimeProvider` (FA-36) — resolves the scheduling gap required between two venues via same-venue policy → directional override → active transfer matrix → conservative fallback, in that precedence order. It depends on `FringeRepository` (for the matrix) and `TransferPolicyOptions` (for overhead/override/fallback config), and caches the loaded matrix for its own (scoped, i.e. per-request) lifetime rather than re-querying per lookup.

**Not yet wired into `ScheduleController`** — the scheduling algorithm's conflict check (`start < s.End && end > s.Start` in `BuildSchedule`/`ComputeMissedShows`) has no venue-transfer awareness today. That integration, plus diagnostics explaining *why* a show was missed due to an infeasible transfer, is a separate ticket (FA-35) — `IVenueTransferTimeProvider` is registered in DI and ready to inject, but nothing calls it yet.

## Environment variables

| Variable            | Purpose                            |
| ------------------- | ---------------------------------- |
| `DYNAMO_TABLE_NAME` | DynamoDB table (default: `fringe`) |
| `ALLOWED_ORIGINS`   | Semicolon-separated CORS origins   |

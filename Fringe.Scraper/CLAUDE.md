# Fringe.Scraper

Scrapes show/venue/showtime data from `tickets.fringetheatre.ca` and writes it to DynamoDB.

## Two entry points, one runner

`ScraperRunner.RunAsync(FringeRepository)` contains all the logic. There are two callers:

- **`Program.cs`** — console entry point for local dev. Reads config from `appSettings.json` and calls the runner directly.
- **`LambdaHandler.cs`** — Lambda entry point (`FringeScraper::FringeScraper.LambdaHandler::FunctionHandler`). Called nightly by EventBridge.

When editing the scraper pipeline, only touch the services in `Services/` and `ScraperRunner.cs`. Don't duplicate logic between the two entry points.

## Running locally

The scraper uses `AmazonDynamoDBClient()` with no args, which resolves credentials from the AWS SDK default chain (env vars, `~/.aws/credentials`, EC2 instance profile, etc.). Set `DYNAMO_TABLE_NAME` or add it to `appSettings.json` under the `DynamoTableName` key to target a specific table.

```bash
dotnet run
```

## Scraper pipeline

`IndexScraper` → list of show IDs  
`DetailScraper` → show metadata + embedded venue/content rating per show  
`ShowTimeFetcher` → showtimes per show (via AJAX endpoint)  
`DatabaseInserter` → `FringeRepository.SaveShowsAsync` + `SaveShowTimesAsync`

Each run **upserts** (overwrites) existing items — it does not delete stale shows. Shows removed from the Fringe website will remain in DynamoDB until manually purged.

## Publish for Lambda

```bash
dotnet publish -c Release -o publish
```

CDK reads from `Fringe.Scraper/publish/`.

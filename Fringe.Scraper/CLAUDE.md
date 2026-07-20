# Fringe.Scraper

Scrapes show/venue/showtime data from `tickets.fringetheatre.ca` and writes it to DynamoDB.

## Two entry points, one runner

`ScraperRunner.RunAsync(FringeRepository)` contains all the logic. There are two callers:

- **`Program.cs`** — console entry point for local dev. Reads config from `appSettings.json` and calls the runner directly.
- **`LambdaHandler.cs`** — Lambda entry point (`FringeScraper::FringeScraper.LambdaHandler::FunctionHandler`). Called nightly by EventBridge.

When editing the scraper pipeline, only touch the services in `Services/` and `ScraperRunner.cs`. Don't duplicate logic between the two entry points.

## Running locally

The scraper uses `AmazonDynamoDBClient()` with no args, which resolves credentials from the AWS SDK default chain (env vars, `~/.aws/credentials`, EC2 instance profile, etc.). Set `DYNAMO_TABLE_NAME` or add it to `appSettings.json` under the `DynamoTableName` key to target a specific table. Set `OPENROUTESERVICE_API_KEY` (or `OpenRouteServiceApiKey` in `appSettings.json`) to enable venue coordinate enrichment — if unset, that stage is skipped with a log line, everything else still runs.

```bash
dotnet run
```

## Scraper pipeline

`IndexScraper` → list of show IDs  
`DetailScraper` → show metadata + deduplicated venues + content ratings per show  
`ShowTimeFetcher` → showtimes per show (via AJAX endpoint)  
`DatabaseInserter` → wraps shows/showtimes/venues in a `FestivalImport` and calls `FringeRepository.SaveShowsAsync` + `SaveShowTimesAsync` + `SaveVenuesAsync` — this is the source-independent import boundary; a future API/CSV/manual importer would populate the same `FestivalImport` shape rather than going through the scraper  
`VenueEnrichmentService` → geocodes canonical venues `FringeRepository.GetVenuesNeedingGeocodingAsync()` reports as eligible (missing coordinates, or a changed routing-relevant address hash), via `IGeocodingProvider` (`OpenRouteServiceGeocodingProvider` in production). Skipped entirely if no provider is configured.

Each run **upserts** (overwrites) existing shows/showtimes — it does not delete stale shows. Shows removed from the Fringe website will remain in DynamoDB until manually purged. Venues are diffed before writing (see `FringeRepository.SaveVenuesAsync`), so an unchanged venue is never rewritten just because its shows changed, and venue coordinates/address-hash/source/enriched-at are never touched by the import path — only `VenueEnrichmentService` (and a manual override, via `FringeRepository.ManualCoordinateSource`) owns those fields.

## Publish for Lambda

```bash
dotnet publish -c Release -o publish
```

CDK reads from `Fringe.Scraper/publish/`.

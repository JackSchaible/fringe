# Fringe.TransferMatrix

Generates directional walking/cycling/driving transfer durations and distances between every
pair of canonical venues, and publishes them as a versioned matrix in DynamoDB for the
scheduling algorithm to consume (see FA-36, not yet implemented).

## Two entry points, one runner

`TransferMatrixRunner.RunAsync(FringeRepository, IMatrixProvider?)` contains all the logic. There are two callers:

- **`Program.cs`** — console entry point for local dev. Reads config from `appSettings.json`/`.env` and calls the runner directly.
- **`LambdaHandler.cs`** — Lambda entry point (`FringeTransferMatrix::FringeTransferMatrix.LambdaHandler::FunctionHandler`). Called nightly by EventBridge, an hour after the scraper's own schedule — the two Lambdas never invoke each other directly, they just agree on a time window.

When editing the pipeline, only touch the services in `Services/` and `TransferMatrixRunner.cs`. Don't duplicate logic between the two entry points.

If `OPENROUTESERVICE_API_KEY` isn't set, both entry points construct a `null` `IMatrixProvider` and the runner logs a message and exits — this is a normal, non-error path (mirrors how `Fringe.Scraper` skips venue enrichment without a key).

## Running locally

Same DynamoDB credential/endpoint resolution as `Fringe.Scraper` (`DYNAMO_TABLE_NAME`, `DYNAMO_ENDPOINT` in `.env`). Set `OPENROUTESERVICE_API_KEY` too, or generation is skipped.

```bash
dotnet run
```

## Pipeline

`TransferMatrixGenerator.GenerateAsync()` (in `Services/`) does the actual work:

1. Load all canonical venues (`FringeRepository.GetAllVenuesAsync`); drop any without coordinates — nothing routable without them.
2. Compute the input hash (`TransferMatrixHasher.ComputeHash`) from the sorted, filtered venue set (venue number + lat/lon only).
3. Compare against `FringeRepository.GetActiveTransferMatrixPointerAsync()`. If the hash is unchanged **and** the active version isn't old enough to need a refresh (30 days by default), exit — no provider calls at all.
4. Otherwise call `IMatrixProvider.GetMatrixAsync` once per mode (walking, cycling, driving) — 3 calls total, regardless of venue count, via OpenRouteService's Matrix API (`OpenRouteServiceMatrixProvider`, distinct from `Fringe.Scraper`'s geocoding client which hits the Search API instead).
5. Validate every mode's matrix has the expected NxN shape and no missing (null) values for any non-self pair. Any failure — provider error, wrong dimensions, a hole in the data — aborts the whole run without writing anything; the previous active version stays active.
6. On success: `SaveTransferMatrixAsync` (metadata + all directional pair records under the new hash), then `SetActiveTransferMatrixAsync` (the actual "publish" — a single item write, so a reader can never observe a half-written version as active), then `MarkTransferMatrixStaleAsync` on the *previous* hash (sets a 30-day TTL so it's cleaned up automatically rather than kept forever).

Each pair record carries all three modes' duration/distance — a 31-venue festival produces 930 pair records (31×30 non-self directional pairs), not 2,790.

## Publish for Lambda

```bash
dotnet publish -c Release -o publish
```

CDK reads from `Fringe.TransferMatrix/publish/`.

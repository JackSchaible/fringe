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
- `IScheduleBuilder` → scoped `ScheduleBuilder`

## Adding endpoints

Add controllers to `Controllers/`. The `FringeRepository` is available for injection — it covers shows, showtimes, votes. See `Fringe.Data/CLAUDE.md` for extending the data layer.

## Services/

Application-layer logic that's more than a repository call but doesn't belong in `Fringe.Data` (no DynamoDB access of its own, or ASP.NET-specific).

- `IVenueTransferTimeProvider`/`VenueTransferTimeProvider` (FA-36) — resolves the scheduling gap required between two venues via same-venue policy → directional override → active transfer matrix → conservative fallback, in that precedence order. It depends on `FringeRepository` (for the matrix) and `TransferPolicyOptions` (for overhead/override/fallback config), and caches the loaded matrix for its own (scoped, i.e. per-request) lifetime rather than re-querying per lookup. Never calls routing/geocoding providers at request time — it only reads DynamoDB.
- `IScheduleBuilder`/`ScheduleBuilder` (FA-11, travel mode added in FA-37) — the schedule-construction algorithm, extracted out of `ScheduleController` so it's directly unit-testable. Greedily books shows in descending-score order, and for each candidate: skips it on a raw time overlap with an already-booked slot, skips it if any group member is unavailable, then calls `IVenueTransferTimeProvider.GetRequiredGapAsync` against its nearest chronological neighbours (found by scanning *all* booked slots by time, not by insertion order, since candidates are booked in score order) to reject candidates the group can't feasibly reach or leave in time. `BuildScheduleAsync` takes an explicit `TravelMode` parameter — one mode applies uniformly to every transfer check in a given build; the scheduler never mixes modes to find a faster transition. A show with no resolvable venue number is looked up using a `-1` sentinel, which — since it never matches a real venue, override, or matrix pair — always falls through to the provider's conservative missing-data fallback rather than silently costing zero minutes. One accepted edge case: two adjacent shows that *both* have unresolvable venues would compare equal and hit the same-venue rule instead of the fallback.

`ScheduleController.GetSchedule` accepts an optional `mode` query string parameter (`walking` | `cycling` | `driving`, case-insensitive; omitted defaults to `walking`; anything else is a `400`) and threads the parsed `TravelMode` into every `BuildScheduleAsync` call for that request (main schedule and each alternate proposal). The resolved mode is echoed back on `ScheduleResponseDto.TravelMode` as a lowercase string so the client can confirm which assumption actually produced the result, rather than trusting its own request unquestioningly.

Venue-transfer-aware "missed show" diagnostics (explaining *why* a show was excluded specifically due to an infeasible transfer, as opposed to a time conflict or availability gap) are not implemented yet — `ComputeMissedShows` in `ScheduleController` is unchanged and transfer-unaware. That's FA-35.

## Environment variables

| Variable            | Purpose                            |
| ------------------- | ---------------------------------- |
| `DYNAMO_TABLE_NAME` | DynamoDB table (default: `fringe`) |
| `ALLOWED_ORIGINS`   | Semicolon-separated CORS origins   |

# Fringe.Data

Shared library used by both `Fringe.API` and `Fringe.Scraper`.

## Namespace quirk

Domain model classes (`Show`, `ShowTime`, `Venue`, `ContentRating`) live in `Models/` but use the namespace `FringeScraper.Models`. This is intentional — the scraper references these types directly without needing a separate namespace alias.

## DynamoDB layer

- `DynamoRecords/` — DynamoDB-annotated record types (`ShowRecord`, `ShowTimeRecord`, `UserRecord`, `UserVoteRecord`). These are persistence types only; domain models in `Models/` are used for in-memory scraping work.
- `FringeRepository.cs` — the only class that touches DynamoDB. Inject `IDynamoDBContext` via DI; the table name comes from the `DYNAMO_TABLE_NAME` env var (default: `fringe`).

## Adding a new entity

1. Add a domain model to `Models/` (if scraper needs it) or skip straight to step 2.
2. Add a `[DynamoDBTable("fringe")]`-annotated record class to `DynamoRecords/`.
3. Add read/write methods to `FringeRepository`.
4. Add the GSI or update the CDK table construct if new access patterns require it.

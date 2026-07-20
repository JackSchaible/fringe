# Fringe.Data

Shared library used by `Fringe.API`, `Fringe.Scraper`, and `Fringe.TransferMatrix`.

## Namespace quirk

Domain model classes (`Show`, `ShowTime`, `Venue`, `ContentRating`) live in `Models/` but use the namespace `FringeScraper.Models`. This is intentional — the scraper references these types directly without needing a separate namespace alias.

## DynamoDB layer

- `DynamoRecords/` — DynamoDB-annotated record types (`ShowRecord`, `ShowTimeRecord`, `VenueRecord`, `TransferMatrixMetadataRecord`, `TransferMatrixPairRecord`, `ActiveTransferMatrixRecord`, `UserRecord`, `UserVoteRecord`). These are persistence types only; domain models in `Models/` are used for in-memory scraping/generation work.
- `FringeRepository.cs` — the only class that touches DynamoDB. Inject `IDynamoDBContext` via DI; the table name comes from the `DYNAMO_TABLE_NAME` env var (default: `fringe`).
- `VenueAddressHasher.cs` — pure normalization/hashing logic shared by `FringeRepository` (no DynamoDB access itself). `VenueRecord` splits field ownership: `SaveVenuesAsync` (import path) only ever writes `Name`/`Address`/`Phone`/`PostalCode`; `Latitude`/`Longitude`/`AddressHash`/`CoordinateSource`/`EnrichedAt` are owned exclusively by `UpdateVenueCoordinatesAsync` (the enrichment path, driven by `Fringe.Scraper`'s `VenueEnrichmentService`, or a manual override via `FringeRepository.ManualCoordinateSource`). Neither path ever overwrites the other's fields.
- `TransferMatrixHasher.cs` — pure normalization/hashing logic for the venue transfer matrix (venue number + latitude + longitude only, rounded to 6 decimal places). Drives `GetActiveTransferMatrixPointerAsync`/`SaveTransferMatrixAsync`/`SetActiveTransferMatrixAsync` in `FringeRepository`, consumed by `Fringe.TransferMatrix`'s `TransferMatrixGenerator`. A version is never promoted (its `CONFIG/ACTIVE_TRANSFER_MATRIX` pointer never flipped) until it's fully written and validated — see `Fringe.TransferMatrix/CLAUDE.md` for the generation/promotion flow.
- `Models/TravelMode.cs`, `Models/TransferGapResult.cs` — shared across `Fringe.TransferMatrix` (which mode a persisted matrix pair's durations belong to) and `Fringe.API` (`Services/VenueTransferTimeProvider.cs`, FA-36 — the read side that turns a matrix lookup into a scheduling decision). Kept here rather than duplicated per-project because both sides need the exact same vocabulary for "which mode" and "why this duration."

## Adding a new entity

1. Add a domain model to `Models/` (if scraper needs it) or skip straight to step 2.
2. Add a `[DynamoDBTable("fringe")]`-annotated record class to `DynamoRecords/`.
3. Add read/write methods to `FringeRepository`.
4. Add the GSI or update the CDK table construct if new access patterns require it.

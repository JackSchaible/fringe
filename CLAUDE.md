# Fringe

Group Fringe-festival schedule planner. Friends log in, vote on shows, and the app algorithmically assembles an optimal group schedule given show times and availability.

## Structure

| Directory         | Role                                                              |
| ----------------- | ----------------------------------------------------------------- |
| `fringe-client/`  | Angular 20 SPA                                                    |
| `Fringe.API/`     | ASP.NET Core .NET 10 Lambda — REST API                            |
| `Fringe.Data/`    | Shared .NET 10 library — DynamoDB models + `FringeRepository`     |
| `Fringe.Scraper/` | .NET 10 Lambda — nightly scraper (also runnable as a console app) |
| `infra/`          | AWS CDK TypeScript — all infrastructure                           |

All .NET projects target **net10.0**. Lambda runtime is `DOTNET_10`.

## Building for deployment

Steps must run in this order — CDK reads the publish output and dist folders at deploy time:

```bash
dotnet publish Fringe.API -c Release -o Fringe.API/publish
dotnet publish Fringe.Scraper -c Release -o Fringe.Scraper/publish
cd fringe-client && npm run build && cd ..
cd infra && npx cdk deploy
```

## Database

DynamoDB on-demand, single table named `fringe`. No SQL, no migrations, no VPC.

Key design: `pk` (string) / `sk` (string). Entity types:

| PK          | SK                        | Entity                                 |
| ----------- | ------------------------- | -------------------------------------- |
| `SHOW#<id>` | `METADATA`                | Show (venue + content rating embedded) |
| `SHOW#<id>` | `SHOWTIME#<iso-datetime>` | ShowTime                               |
| `USER#<id>` | `PROFILE`                 | User                                   |
| `USER#<id>` | `VOTE#SHOW#<showId>`      | UserVote                               |

GSI `entity-type-index`: pk=`entityType`, sk=`pk` — used to list all shows.

## Domain

`fringe.jackschaible.ca` (external registrar — DNS CNAMEs are added manually after deploy).

## Verification before finishing a change

Every project here has lint, format, and test checks that all run as required GitHub Actions status checks on `main` (`lint`, `format`, `test`, `coverage`, `audit`) — a change that doesn't pass locally will fail CI. Run the relevant ones for whatever you touched before calling a task done:

```bash
# From repo root — runs all three projects
pnpm run lint      # dotnet format --verify-no-changes, ng lint, eslint
pnpm run format    # dotnet format --verify-no-changes, prettier --check (client + infra)
pnpm run test      # dotnet test, ng test, jest

# Or per-project
dotnet build fringe.sln && dotnet test fringe.sln   # Fringe.API / Fringe.Data / Fringe.Scraper
cd fringe-client && pnpm run lint && pnpm run format:check && pnpm test
cd infra && pnpm run lint && pnpm run format:check && pnpm test
```

Don't trust an aggregate pass/fail count alone when a change is supposed to add or remove a specific test — grep the output for the actual test name to confirm it ran.

## C#/.NET guidance

`Directory.Build.props` sets `AnalysisMode: AllEnabledByDefault` with `TreatWarningsAsErrors`/`CodeAnalysisTreatWarningsAsErrors` — every analyzer category (including Security) is a build-breaking error, not a suggestion. When resolving an analyzer diagnostic or reasoning about ASP.NET Core, JWT bearer, or Cognito behavior, check official Microsoft documentation (learn.microsoft.com) rather than relying on general recollection — a plausible-looking fix for one diagnostic can silently break correctness elsewhere. Concretely: swapping `TokenValidationParameters.ValidateAudience = false` for `options.Audience = clientId` satisfies `CA5404`, but Cognito **access tokens** (what this frontend sends) carry a `client_id` claim, not the standard `aud` claim `Audience` checks — that swap took prod auth down for every request. Verify the actual token/claim shape and the analyzer's documented rationale before picking a fix, not just whichever change makes the compiler quiet.

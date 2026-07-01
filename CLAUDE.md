# Fringe

Group Fringe-festival schedule planner. Friends log in, vote on shows, and the app algorithmically assembles an optimal group schedule given show times and availability.

## Structure

| Directory | Role |
| --- | --- |
| `fringe-client/` | Angular 20 SPA |
| `Fringe.API/` | ASP.NET Core .NET 10 Lambda — REST API |
| `Fringe.Data/` | Shared .NET 10 library — DynamoDB models + `FringeRepository` |
| `Fringe.Scraper/` | .NET 10 Lambda — nightly scraper (also runnable as a console app) |
| `infra/` | AWS CDK TypeScript — all infrastructure |

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

| PK | SK | Entity |
|---|---|---|
| `SHOW#<id>` | `METADATA` | Show (venue + content rating embedded) |
| `SHOW#<id>` | `SHOWTIME#<iso-datetime>` | ShowTime |
| `USER#<id>` | `PROFILE` | User |
| `USER#<id>` | `VOTE#SHOW#<showId>` | UserVote |

GSI `entity-type-index`: pk=`entityType`, sk=`pk` — used to list all shows.

## Domain

`fringe.jackschaible.ca` (external registrar — DNS CNAMEs are added manually after deploy).

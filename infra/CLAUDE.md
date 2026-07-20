# infra

AWS CDK TypeScript project. Deploys the full Fringe stack.

## Prerequisites

The CDK reads build artifacts at deploy time. Build them first:

```bash
# From repo root:
dotnet publish Fringe.API -c Release -o Fringe.API/publish
dotnet publish Fringe.Scraper -c Release -o Fringe.Scraper/publish
dotnet publish Fringe.TransferMatrix -c Release -o Fringe.TransferMatrix/publish
cd fringe-client && npm run build && cd ..
```

## Deploy

```bash
npm install
npx cdk bootstrap   # first time per account/region only
npx cdk deploy
```

## Construct layout

| File                            | What it creates                                                                                                                                                                                                                                        |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `constructs/dynamo.ts`          | DynamoDB `TableV2` (on-demand) + `entity-type-index` GSI                                                                                                                                                                                               |
| `constructs/api.ts`             | Lambda (Fringe.API) + REST API Gateway + custom domain                                                                                                                                                                                                 |
| `constructs/scraper.ts`         | Lambda (Fringe.Scraper) + nightly EventBridge rule                                                                                                                                                                                                     |
| `constructs/transfer-matrix.ts` | Lambda (Fringe.TransferMatrix) + nightly EventBridge rule, an hour after the scraper's; `reservedConcurrentExecutions: 1`                                                                                                                              |
| `constructs/frontend.ts`        | S3 bucket + CloudFront OAC distribution + ACM cert + Route53 alias records + a `BucketDeployment` per locale (en-CA at bucket root, others under `/<url-locale>/`) + a CloudFront Function that routes app requests to the right locale's `index.html` |
| `lib/fringe-stack.ts`           | Root stack — wires constructs, provisions cert + hosted zone, outputs DNS values                                                                                                                                                                       |

## ACM certificate & hosted zone

`FringeCertStack` (pinned to `us-east-1`, since CloudFront requires ACM certs in that region) provisions both:

- A Route53 `HostedZone` for `fringequest.app`
- A `Certificate` covering `fringequest.app` and `api.fringequest.app`, validated via DNS records CDK creates automatically in that hosted zone

`FringeStack` receives both via props (`crossRegionReferences: true` on both stacks wires the cross-region reference). `FringeFrontend` and `FringeApi` each create Route53 alias records (CloudFront apex A/AAAA, API Gateway `api` A record) directly in the hosted zone — no manual CNAMEs needed after the initial nameserver handoff below.

## DNS (one-time manual step — external registrar)

Route53 can't take over a domain's DNS until the registrar points at its nameservers:

1. `cdk deploy` the `FringeCertStack` (or the full app — order doesn't matter, but the cert stack must synth first to create the hosted zone).
2. Read the `NameServers` output from `FringeCertStack`.
3. At fringequest.app's registrar, set those 4 values as the domain's nameservers (this replaces the registrar's default DNS, not just a record — it hands the whole zone to Route53).
4. Wait for propagation (usually fast, can take up to 48h). Once it resolves, ACM DNS validation, the CloudFront alias, and the API Gateway alias all complete automatically — CDK manages the records, nothing else to add by hand.

`fringequest.app` is an apex/root domain, which is why alias records (not CNAMEs) are used for the CloudFront and API Gateway targets — plain DNS forbids a CNAME at the zone apex, but Route53 ALIAS records work at the apex.

## Table removal policy

The DynamoDB table uses `RemovalPolicy.RETAIN` — `cdk destroy` will not delete the table or its data.

TTL is enabled on the `ttl` attribute (`timeToLiveAttribute: "ttl"` in `constructs/dynamo.ts`). Only superseded transfer-matrix versions currently set it, so this is otherwise inert — don't assume any other item type auto-expires.

## CI/CD — one-time AWS setup (OIDC)

The GitHub Actions workflow in `.github/workflows/deploy.yml` uses OIDC to assume an IAM role — no long-lived credentials are stored in GitHub secrets. This setup needs to happen once per AWS account:

**1. Create the GitHub OIDC provider** (skip if already done for other repos):

```bash
aws iam create-open-id-connect-provider \
  --url https://token.actions.githubusercontent.com \
  --client-id-list sts.amazonaws.com \
  --thumbprint-list 6938fd4d98bab03faadb97b34396831e3780aea1
```

**2. Create the IAM role** with this trust policy (saves as `trust.json`):

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Federated": "arn:aws:iam::<ACCOUNT_ID>:oidc-provider/token.actions.githubusercontent.com"
      },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
          "token.actions.githubusercontent.com:aud": "sts.amazonaws.com",
          "token.actions.githubusercontent.com:sub": "repo:JackSchaible/fringe:ref:refs/heads/main"
        }
      }
    }
  ]
}
```

```bash
aws iam create-role --role-name fringe-github-deploy --assume-role-policy-document file://trust.json
aws iam attach-role-policy --role-name fringe-github-deploy --policy-arn arn:aws:iam::aws:policy/AdministratorAccess
```

**3. Add the role ARN as a GitHub secret:**

In the `JackSchaible/fringe` repo → Settings → Secrets → Actions:

- `AWS_DEPLOY_ROLE_ARN` = `arn:aws:iam::<ACCOUNT_ID>:role/fringe-github-deploy`

That's it. Every push to `main` will now deploy automatically.

## Other GitHub secrets read by the deploy workflow

Same Settings → Secrets → Actions page as above:

| Secret                     | Consumed by                                                                                                                | Purpose                                                                                                                                                                                                                                |
| -------------------------- | -------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `TURNSTILE_SITE_KEY`       | `environment.prod.ts` (frontend build step)                                                                                | Cloudflare Turnstile client-side site key                                                                                                                                                                                              |
| `TURNSTILE_SECRET_KEY`     | `constructs/api.ts` (Fringe.API Lambda env)                                                                                | Cloudflare Turnstile server-side verification                                                                                                                                                                                          |
| `OPENROUTESERVICE_API_KEY` | `constructs/scraper.ts` (Fringe.Scraper Lambda env) and `constructs/transfer-matrix.ts` (Fringe.TransferMatrix Lambda env) | OpenRouteService geocoding (FA-33) and matrix (FA-34) key, shared by both Lambdas. Unset means the Lambda env var is an empty string, so venue enrichment / matrix generation is skipped at runtime — deploy still succeeds either way |

`OPENROUTESERVICE_API_KEY` isn't provisioned anywhere by this repo — get a free-tier key from [openrouteservice.org/dev/#/signup](https://openrouteservice.org/dev/#/signup) and add it as a secret manually.

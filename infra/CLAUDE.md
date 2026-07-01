# infra

AWS CDK TypeScript project. Deploys the full Fringe stack.

## Prerequisites

The CDK reads build artifacts at deploy time. Build them first:

```bash
# From repo root:
dotnet publish Fringe.API -c Release -o Fringe.API/publish
dotnet publish Fringe.Scraper -c Release -o Fringe.Scraper/publish
cd fringe-client && npm run build && cd ..
```

## Deploy

```bash
npm install
npx cdk bootstrap   # first time per account/region only
npx cdk deploy
```

## Construct layout

| File | What it creates |
|---|---|
| `constructs/dynamo.ts` | DynamoDB `TableV2` (on-demand) + `entity-type-index` GSI |
| `constructs/api.ts` | Lambda (Fringe.API) + REST API Gateway + custom domain |
| `constructs/scraper.ts` | Lambda (Fringe.Scraper) + nightly EventBridge rule |
| `constructs/frontend.ts` | S3 bucket + CloudFront OAC distribution + ACM cert + `BucketDeployment` |
| `lib/fringe-stack.ts` | Root stack — wires constructs, provisions cert, outputs DNS values |

## ACM certificate

The cert covers `fringe.jackschaible.ca` and `api.fringe.jackschaible.ca`. CloudFront requires ACM certs in `us-east-1`; `crossRegionReferences: true` on the stack handles the cross-region reference automatically — no separate stack needed.

Validation is DNS-based. After the first deploy, CDK outputs a CNAME record to add at the registrar. The deploy will wait (up to 30 min) until the cert is validated.

## DNS (manual — external registrar)

After `cdk deploy`, add these records at the registrar for `jackschaible.ca`:

1. **ACM validation** — CNAME shown in the CloudFormation console (one-time only)
2. `CNAME fringe → <value of CloudFrontDomain output>`
3. `CNAME api.fringe → <value of ApiGatewayDomain output>`

## Table removal policy

The DynamoDB table uses `RemovalPolicy.RETAIN` — `cdk destroy` will not delete the table or its data.

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
  "Statement": [{
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
  }]
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

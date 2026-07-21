# e2e

Playwright end-to-end tests that run against the live deployed site (`https://fringequest.app` by default). Not part of the PR `test` gate — there's nothing to deploy against until `main` has already shipped, so these only run post-deploy in `deploy.yml`.

Currently just a smoke test (`tests/smoke.spec.ts`) confirming the app shell renders after a deploy. More e2e coverage will be added over time.

## Running locally

```bash
cd e2e
pnpm install
pnpm exec playwright install --with-deps chromium   # first time only
pnpm test                              # against production
E2E_BASE_URL=https://localhost:4200 pnpm test   # against a local dev server
```

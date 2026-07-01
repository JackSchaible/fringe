# fringe-client

Angular 20 SPA. Talks to `Fringe.API` at `api.fringe.jackschaible.ca`.

## Dev server

`npm start` runs the Angular dev server with SSL enabled. It requires local certificates:

1. Create `.cert/` in this directory (already gitignored).
2. Generate `localhost.crt` and `localhost.key` (e.g. via `mkcert localhost`).
3. Set paths in `.env`:

```
SSL_CERT_PATH=.cert/localhost.crt
SSL_KEY_PATH=.cert/localhost.key
```

Then: `npm start` → `https://localhost:4200`

## Build for deployment

```bash
npm run build
```

Output lands in `dist/fringe-client/browser/`. CDK's `BucketDeployment` in `infra/constructs/frontend.ts` reads from this exact path — don't change it without updating the construct.

## Routing

The app uses Angular Router with HTML5 history. CloudFront is configured to return `index.html` on 403/404, so deep-links work after deploy. The dev server handles this automatically.

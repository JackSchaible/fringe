# fringe-client

Angular 20 SPA. Talks to `Fringe.API` at `api.fringequest.app`.

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

The production configuration builds every configured locale (see i18n below), so output lands in one subfolder per locale: `dist/client-new/browser/en-CA/`, `dist/client-new/browser/fr-CA/`, etc. CDK's `BucketDeployment`s in `infra/lib/constructs/frontend.ts` read from these exact paths — don't change the folder layout without updating the construct.

## Routing

The app uses Angular Router with HTML5 history. A CloudFront Function rewrites app-route requests (no file extension) to the matching locale's `index.html`, so deep-links work for both `/` (en-CA) and `/fr/*` after deploy. The dev server handles this automatically since it only ever serves one locale — see the i18n note below on why the language switcher can't be exercised under `ng serve`.

## i18n / translations

Templates mark translatable text with `i18n="@@some.id"` / `i18n-someattr="@@some.id"` (custom message IDs, so extraction is stable across refactors). This is Angular's built-in `@angular/localize`, compiled at build time — there's no runtime language switch; each locale is a fully separate bundle, and `AppComponent.switchLocaleHref()` (`app.ts`) does a real page navigation to the same path under the other locale's URL prefix rather than a client-side route change.

- **Extract strings** after adding/changing `i18n` markup: `npx ng extract-i18n` (or `npm run` equivalent) regenerates `src/locale/messages.xlf` from every `i18n`-tagged string in the templates.
- **Translate** by adding a `<target>` element inside each `<unit>`'s `<segment>` in `src/locale/messages.<locale>.xlf` (e.g. `src/locale/messages.fr-CA.xlf`), next to the existing `<source>`. Any inline `<ph>`/`<pc>` placeholder tags (interpolations, embedded elements, `@if`/`@else` blocks) must appear in the target exactly once with identical `id`s — Angular's build fails if a target is missing a placeholder the source has.
- **Locale codes vs. URL prefixes are decoupled.** `angular.json`'s `i18n.sourceLocale`/`i18n.locales` entries can be objects with a `baseHref` override so the locale-aware pipes (dates, numbers) use the full regional code (`en-CA`, `fr-CA`) while the URL stays short (`/`, `/fr/`). The source locale's `baseHref` **must** be an explicit `"/"`, not `""` — an empty override resolves relative asset URLs against the current document path, which breaks on multi-segment routes like `/auth/callback`.
- **Add a new locale**: add an entry under `i18n.locales` in `angular.json` (with a `baseHref` if you want a short URL prefix), create the corresponding `src/locale/messages.<locale>.xlf`, and add a case to the CloudFront Function in `infra/lib/constructs/frontend.ts` (it currently only special-cases `/fr/`) plus a second `BucketDeployment` for that locale's output folder.
- `ng serve` / `npm start` only ever builds the source locale (`en-CA`) — you don't need translations filled in to develop, but it also means the language switcher has nothing to switch _to_ locally; it'll 404/redirect back via the wildcard route. To exercise it, run `npm run build` and serve `dist/client-new/browser/` with a static server that replicates the CloudFront Function's rewrite (extension-less requests under `/fr/` → `/fr/index.html`, everything else → `/index.html`).

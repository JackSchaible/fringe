#!/usr/bin/env node
'use strict';

// `ng serve` only ever compiles the source locale, so there's no way to
// exercise the language switcher (app.ts `switchLocaleHref()`) against a
// real dev server. This replicates, locally, the same request rewrite the
// CloudFront Function in infra/lib/constructs/frontend.ts applies in
// production: en-CA serves unprefixed from the bucket root, fr-CA serves
// under /fr/, and extension-less requests fall back to the matching
// locale's index.html for client-side routing. Run `npm run build` first.

const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');

const PORT = process.env.PORT || 4300;
const BROWSER_DIR = path.join(__dirname, '..', 'dist', 'client-new', 'browser');
const FR_PREFIX = '/fr';

const MIME_TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
  '.webmanifest': 'application/manifest+json',
  '.txt': 'text/plain; charset=utf-8',
};

function resolveFile(pathname) {
  const lastSegment = pathname.slice(pathname.lastIndexOf('/') + 1);
  const hasExtension = lastSegment.includes('.');
  const isFrench =
    pathname === FR_PREFIX || pathname.startsWith(`${FR_PREFIX}/`);

  if (hasExtension) {
    return isFrench
      ? path.join(BROWSER_DIR, 'fr-CA', pathname.slice(FR_PREFIX.length + 1))
      : path.join(BROWSER_DIR, 'en-CA', pathname.slice(1));
  }

  return isFrench
    ? path.join(BROWSER_DIR, 'fr-CA', 'index.html')
    : path.join(BROWSER_DIR, 'en-CA', 'index.html');
}

if (!fs.existsSync(BROWSER_DIR)) {
  console.error(
    `${BROWSER_DIR} doesn't exist yet — run "npm run build" first.`,
  );
  process.exit(1);
}

http
  .createServer((req, res) => {
    const pathname = decodeURIComponent(req.url.split('?')[0]);
    const filePath = resolveFile(pathname);

    fs.readFile(filePath, (err, data) => {
      if (err) {
        res.writeHead(404, { 'Content-Type': 'text/plain' });
        res.end(`Not found: ${filePath}`);
        return;
      }
      const contentType =
        MIME_TYPES[path.extname(filePath)] || 'application/octet-stream';
      res.writeHead(200, { 'Content-Type': contentType });
      res.end(data);
    });
  })
  .listen(PORT, () => {
    console.log(
      `Locale-aware preview server running at http://localhost:${PORT}`,
    );
  });

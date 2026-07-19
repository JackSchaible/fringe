#!/usr/bin/env node
'use strict';

// Cross-platform replacement for the old test-all.sh — pnpm on Windows runs
// package.json scripts through cmd.exe, not bash, even when invoked from a
// Git Bash terminal, so a `#!/bin/bash` script with `./` invocation and
// `eval` failed there. Node runs identically everywhere `pnpm test` runs.

const { mkdirSync, openSync } = require('node:fs');
const { spawnSync } = require('node:child_process');
const path = require('node:path');

const COVERAGE_DIR = 'coverage';
const SUCCESS_STATUS = 0;
const FAILURE_STATUS = 1;

function run(command, args, logFile) {
  const fd = openSync(logFile, 'w');
  const result = spawnSync(command, args, {
    stdio: ['ignore', fd, fd],
    shell: true,
  });
  return result.status ?? FAILURE_STATUS;
}

mkdirSync(COVERAGE_DIR, { recursive: true });

// infra's frontend/fringe-stack tests synthesize a BucketDeployment that
// reads this build output from disk, so it must exist before test:infra runs.
console.log('Building fringe-client...');
const buildStatus = run(
  'pnpm',
  ['--dir', 'fringe-client', 'run', 'build'],
  path.join(COVERAGE_DIR, 'build-client.log'),
);
if (buildStatus !== SUCCESS_STATUS) {
  console.log('  FAIL  build:client  (see coverage/build-client.log)');
  process.exit(FAILURE_STATUS);
}

const names = ['server', 'client', 'infra'],
  results = {};

for (const name of names) {
  console.log(`Running test:${name}...`);
  results[name] = run(
    'pnpm',
    ['run', `test:${name}`],
    path.join(COVERAGE_DIR, `test-${name}.log`),
  );
}

console.log('\nSummary:');
let failed = SUCCESS_STATUS;
for (const name of names) {
  if (results[name] === SUCCESS_STATUS) {
    console.log(`  PASS  ${name}`);
  } else {
    console.log(`  FAIL  ${name}  (see coverage/test-${name}.log)`);
    failed = FAILURE_STATUS;
  }
}

process.exit(failed);

#!/usr/bin/env node
"use strict";

// Cross-platform replacement for `cross-var` — substitutes $VAR / %VAR% in
// argv with process.env values, then spawns the resulting command. Kept as
// a tiny local script because cross-var is abandoned and pulls in Babel 6
// tooling (babel-register/babel-preset-es2015) with an unfixable critical
// CVE (GHSA-67hx-6x53-jw92).

const { spawnSync } = require("node:child_process");

function expand(arg) {
  return Object.keys(process.env)
    .sort((first, second) => second.length - first.length)
    .reduce(
      (value, key) =>
        value.replace(new RegExp(`\\$${key}|%${key}%`, "gi"), process.env[key]),
      arg,
    );
}

const [command, ...args] = process.argv.slice(2).map(expand);
const result = spawnSync(command, args, { stdio: "inherit", shell: true });
process.exit(result.status ?? 1);

// @ts-check
const eslint = require("@eslint/js");
const tseslint = require("typescript-eslint");

// Restates typescript-eslint's own naming-convention defaults (see
// defaultCamelCaseAllTheThingsConfig upstream) since providing any options
// here replaces rather than extends them, then carves out property/method
// names dictated by external API shapes we don't control: `Auth`/`Cognito`
// (aws-amplify's ResourcesConfig), `expired-callback`/`error-callback`
// (Cloudflare Turnstile's JS API), the PascalCase fields of
// @aws-sdk/client-ses's SendEmailCommandInput (Source/Destination/
// ToAddresses/Message/Subject/Body/Text/Data), and Lambda `environment`
// blocks (OS/process.env-convention SCREAMING_SNAKE_CASE, read by name on
// the .NET side — open-ended, so matched by pattern rather than enumerated).
/** @type {["error", ...unknown[]]} */
const namingConvention = [
  "error",
  {
    selector: "default",
    format: ["camelCase"],
    leadingUnderscore: "allow",
    trailingUnderscore: "allow",
  },
  { selector: "import", format: ["camelCase", "PascalCase"] },
  {
    selector: "variable",
    format: ["camelCase", "UPPER_CASE"],
    leadingUnderscore: "allow",
    trailingUnderscore: "allow",
  },
  { selector: "typeLike", format: ["PascalCase"] },
  {
    selector: "objectLiteralProperty",
    filter: {
      regex:
        "^(Auth|Cognito|Source|Destination|ToAddresses|Message|Subject|Body|Text|Data)$",
      match: true,
    },
    format: null,
  },
  {
    selector: "objectLiteralProperty",
    filter: { regex: "^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$", match: true },
    format: null,
  },
  {
    selector: ["typeMethod", "objectLiteralMethod"],
    filter: { regex: "^(expired-callback|error-callback)$", match: true },
    format: null,
  },
];

module.exports = tseslint.config(
  {
    ignores: ["dist/**", "bin/**"],
  },
  {
    files: ["**/*.ts"],
    extends: [eslint.configs.all, ...tseslint.configs.all],
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: __dirname,
      },
    },
    rules: {
      "@typescript-eslint/array-type": [
        "error",
        { default: "generic", readonly: "generic" },
      ],
      "@typescript-eslint/no-explicit-any": "error",
      "@typescript-eslint/no-unused-vars": [
        "error",
        { argsIgnorePattern: "^_" },
      ],
      "@typescript-eslint/naming-convention": namingConvention,
      // Conflicts with @typescript-eslint/no-floating-promises, whose own docs
      // recommend `void` as the way to mark a promise as intentionally unhandled.
      "no-void": "off",
      // Conflicts with @typescript-eslint/promise-function-async, which forces
      // any function whose declared/inferred return type includes Promise to
      // be `async` — including handlers (e.g. Lambda triggers) that fulfil a
      // Promise-returning interface but have nothing to await internally.
      "@typescript-eslint/require-await": "off",
      // sort-vars forces alphabetical order within a single comma-separated
      // declaration list. That fights the two orderings that actually make a
      // list readable: grouping semantically related declarators together,
      // and ordering by dependency when one declarator (a helper used by a
      // test group defined after it, say) relies on another declared earlier
      // in the same list — alphabetical order can contradict either one.
      "sort-vars": "off",
      // one-var (default "always") forces every const/let/var in a scope into
      // a single declaration statement, even when they're unrelated or one is
      // computed from imperative statements between the others — not
      // achievable without contorting straightforward code.
      "one-var": "off",
      // sort-keys forces alphabetical object-literal key order, which fights
      // object literals that must mirror an external, documented field order
      // (AWS Cognito trigger events, SES SendEmailCommandInput) — alphabetical
      // order there only makes them harder to diff against the source docs.
      "sort-keys": "off",
      // require-atomic-updates is known to false-positive on `obj.prop = value`
      // assignments that merely follow an unrelated `await` earlier in the
      // function, even when `value` doesn't depend on `obj`'s current state
      // and there's no actual concurrent mutation (e.g. Lambda handlers are
      // invoked one at a time, never concurrently against the same event).
      "require-atomic-updates": "off",
      "@typescript-eslint/prefer-readonly-parameter-types": [
        "error",
        {
          ignoreInferredTypes: true,
          allow: [
            {
              from: "lib",
              name: ["Date", "Event", "Readonly", "ReadonlyArray"],
            },
            // CDK construct classes carry internal (near-private) state that
            // makes a mapped Readonly<T> wrapper structurally incompatible
            // with the concrete class — passing one where the real type is
            // expected then fails to compile. Trusted as-is, same as the
            // other framework types above.
            {
              from: "package",
              package: "aws-cdk-lib/aws-certificatemanager",
              name: ["Certificate"],
            },
          ],
        },
      ],
    },
  },
  {
    files: ["**/*.test.ts"],
    rules: {
      // expect(spy).toHaveBeenCalled() is a standard Jest spy assertion — not
      // an unbound-callback hazard, since it's never invoked without its
      // receiver. The rule can't tell the two apart; upstream typescript-eslint
      // addresses this in Jest projects by swapping in eslint-plugin-jest's
      // test-aware `jest/unbound-method` rule for test files.
      "@typescript-eslint/unbound-method": "off",
    },
  },
  {
    files: ["lambda/**/*.ts"],
    rules: {
      // Lambda handlers log via console.log/console.error — that output goes
      // straight to CloudWatch and is the standard way to log from a Lambda,
      // not leftover debug output.
      "no-console": "off",
    },
  },
  {
    files: ["lib/**/*.ts", "test/**/*.ts"],
    rules: {
      // AWS CDK constructs register themselves in the construct tree purely
      // via `new XConstruct(this, id, props)` — the return value is
      // routinely left unused. That's the idiomatic pattern for the whole
      // framework (test setup instantiates stacks/constructs the same way),
      // not a discarded side effect to flag.
      "no-new": "off",
    },
  },
  {
    files: ["test/**/*.ts"],
    rules: {
      "@typescript-eslint/naming-convention": [
        ...namingConvention,
        // Template.hasResourceProperties()/Match.objectLike() assertions
        // mirror raw CloudFormation JSON, which is PascalCase throughout
        // (Runtime, Handler, Environment, PolicyDocument, ...) — an external,
        // ever-growing contract we don't control, so matched generically
        // rather than enumerated.
        {
          selector: "objectLiteralProperty",
          filter: { regex: "^[A-Z][a-zA-Z0-9]*$", match: true },
          format: null,
        },
      ],
      // `let x: T;` declared here and assigned in a `beforeEach` is the
      // standard CDK/Jest test-setup pattern (the value can only be computed
      // once the stack under test is built for that case) — not an
      // uninitialized-variable hazard.
      "@typescript-eslint/init-declarations": "off",
      // describe/it blocks for CDK template assertions are naturally long —
      // one `describe` per construct area with many focused `it`s and the
      // shared `beforeEach` setup. Length here reflects assertion count, not
      // complexity worth splitting up.
      "max-lines-per-function": "off",
      // Expected resource/element counts (resourceCountIs(..., 1),
      // toHaveLength(4)) are the literal value under test, not a magic
      // number standing in for an unexplained threshold.
      "@typescript-eslint/no-magic-numbers": "off",
      // A `describe` block's statement count is really "how many `it`s/
      // `beforeEach`s does this suite have" — one statement per test case.
      // A thorough handler/construct test suite legitimately has more than
      // 10 of those; that's coverage, not a function doing too much.
      "max-statements": "off",
    },
  },
);

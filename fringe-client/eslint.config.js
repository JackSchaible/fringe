// @ts-check
const eslint = require("@eslint/js");
const tseslint = require("typescript-eslint");
const angular = require("angular-eslint");

// @angular-eslint/sort-keys-in-type-decorator defaults to a semantic grouping
// (selector, imports, templateUrl, ...) instead of alphabetical, which fights
// core `sort-keys`. Sorting its own default order alphabetically makes the two
// rules agree, so `sort-keys` can stay on for the rest of the codebase too.
const alphabeticalDecoratorOrder = {
  Component: [
    "selector",
    "imports",
    "standalone",
    "templateUrl",
    "template",
    "styleUrl",
    "styleUrls",
    "styles",
    "providers",
    "changeDetection",
    "encapsulation",
    "viewProviders",
    "host",
    "hostDirectives",
    "inputs",
    "outputs",
    "animations",
    "schemas",
    "exportAs",
    "queries",
    "preserveWhitespaces",
    "jit",
    "moduleId",
    "interpolation",
  ].sort(),
  Directive: [
    "selector",
    "standalone",
    "providers",
    "host",
    "hostDirectives",
    "inputs",
    "outputs",
    "exportAs",
    "queries",
    "jit",
  ].sort(),
  NgModule: [
    "id",
    "imports",
    "declarations",
    "providers",
    "exports",
    "bootstrap",
    "schemas",
    "jit",
  ].sort(),
  Pipe: ["name", "standalone", "pure"].sort(),
};

module.exports = tseslint.config(
  {
    ignores: ["dist/**", ".angular/**"],
  },
  {
    files: ["**/*.ts"],
    extends: [
      eslint.configs.all,
      ...tseslint.configs.all,
      ...angular.configs.tsAll,
    ],
    processor: angular.processInlineTemplates,
    languageOptions: {
      parserOptions: {
        project: [
          "./tsconfig.json",
          "./tsconfig.app.json",
          "./tsconfig.spec.json",
        ],
        createDefaultProgram: true,
      },
    },
    rules: {
      "@angular-eslint/directive-selector": [
        "error",
        {
          type: "attribute",
          prefix: "fg",
          style: "camelCase",
        },
      ],
      "@angular-eslint/component-selector": [
        "error",
        {
          type: "element",
          prefix: "fg",
          style: "kebab-case",
        },
      ],
      "@angular-eslint/component-class-suffix": [
        "error",
        { suffixes: ["Component", "Page"] },
      ],
      "@typescript-eslint/array-type": [
        "error",
        { default: "generic", readonly: "generic" },
      ],
      "@typescript-eslint/no-explicit-any": "error",
      "@typescript-eslint/no-unused-vars": [
        "error",
        { argsIgnorePattern: "^_" },
      ],
      "@angular-eslint/no-developer-preview": "off",
      // new-cap fires on every @Component/@Injectable/@Directive decorator
      // (uppercase function call without `new`) — incompatible with decorators.
      "new-cap": "off",
      // A template-only page component (e.g. PrivacyPage) is legitimately an
      // empty class — Angular requires the class to exist to hang the
      // @Component decorator on. allowWithDecorator keeps the rule active for
      // genuinely extraneous (non-decorated) empty classes elsewhere.
      "@typescript-eslint/no-extraneous-class": [
        "error",
        { allowWithDecorator: true },
      ],
      "@angular-eslint/sort-keys-in-type-decorator": [
        "error",
        alphabeticalDecoratorOrder,
      ],
      // Restates typescript-eslint's own naming-convention defaults (see
      // defaultCamelCaseAllTheThingsConfig upstream) since providing any
      // options here replaces rather than extends them, then carves out
      // property/method names dictated by external API shapes we don't
      // control: `Auth`/`Cognito` (aws-amplify's ResourcesConfig) and
      // `expired-callback`/`error-callback` (Cloudflare Turnstile's JS API).
      "@typescript-eslint/naming-convention": [
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
          filter: { regex: "^(Auth|Cognito)$", match: true },
          format: null,
        },
        {
          selector: ["typeMethod", "objectLiteralMethod"],
          filter: { regex: "^(expired-callback|error-callback)$", match: true },
          format: null,
        },
      ],
      // Conflicts with @typescript-eslint/no-floating-promises, whose own docs
      // recommend `void` as the way to mark a promise as intentionally unhandled.
      "no-void": "off",
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
      // FullCalendar's callback-argument types (and EventImpl, its event class),
      // Angular/CDK/testing framework types, and DOM lib built-ins all have
      // mutator methods we don't control — no type annotation can make them
      // deeply readonly, so these specific types are trusted as-is. Only
      // explicitly-annotated parameters are checked at all (ignoreInferredTypes)
      // since checking inferred callback params — .then(m => ...), .subscribe(x => ...) —
      // would otherwise flag framework-supplied types we have no way to annotate.
      "@typescript-eslint/prefer-readonly-parameter-types": [
        "error",
        {
          ignoreInferredTypes: true,
          allow: [
            {
              from: "package",
              package: "@fullcalendar/core",
              name: ["EventClickArg", "DateSelectArg"],
            },
            {
              from: "package",
              package: "@fullcalendar/core/internal",
              name: ["EventImpl"],
            },
            {
              from: "package",
              package: "@angular/core",
              // Signal's internal branded symbol points to a mutable reactive
              // node, which the rule's structural check sees regardless of
              // Readonly<> wrapping — even though nothing in application code
              // can reach or mutate it through the Signal<T> read interface.
              name: ["ComponentFixture", "Signal"],
            },
            {
              from: "package",
              package: "@angular/cdk",
              name: ["CdkDragDrop"],
            },
            {
              from: "package",
              package: "@types/jasmine",
              name: ["SpyObj"],
            },
            {
              from: "package",
              package: "@angular/common",
              name: ["HttpErrorResponse"],
            },
            {
              from: "lib",
              name: [
                "Date",
                "HTMLElement",
                "Event",
                "Readonly",
                "ReadonlyArray",
              ],
            },
          ],
        },
      ],
    },
  },
  {
    files: ["**/*.spec.ts"],
    rules: {
      // expect(spy.method).toHaveBeenCalled() is the standard Jasmine spy
      // assertion — not an unbound-callback hazard, since it's never invoked
      // without its receiver. The rule can't tell the two apart; upstream
      // typescript-eslint addresses this in Jest projects by swapping in
      // eslint-plugin-jest's test-aware `jest/unbound-method` for spec files.
      // No Jasmine equivalent exists, so we scope the base rule off here.
      "@typescript-eslint/unbound-method": "off",
    },
  },
  {
    files: ["**/*.html"],
    extends: [...angular.configs.templateAll],
    rules: {
      // TODO: re-check this — the rule can't tell a signal read (`mySignal()`,
      // cheap and memoized) from an arbitrary method call in a template
      // (potentially expensive, re-run every change detection cycle), so it
      // flags every signal usage. Revisit if the rule/tooling adds signal
      // awareness, or if we want to audit for genuine expensive-call cases.
      "@angular-eslint/template/no-call-expression": "off",
    },
  },
);

module.exports = {
  testEnvironment: "node",
  roots: ["<rootDir>/test"],
  testMatch: ["**/*.test.ts"],
  collectCoverageFrom: ["lib/**/*.ts", "lambda/**/*.ts"],
  coverageDirectory: "<rootDir>/coverage",
  coverageReporters: ["text", "lcov", "cobertura", "html"],
  transform: {
    "^.+\\.tsx?$": ["ts-jest", { tsconfig: "tsconfig.test.json" }],
  },
};

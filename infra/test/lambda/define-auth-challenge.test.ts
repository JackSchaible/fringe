import type { Context, DefineAuthChallengeTriggerEvent } from "aws-lambda";
import { handler } from "../../lambda/auth/define-auth-challenge";

type SessionEntry = DefineAuthChallengeTriggerEvent["request"]["session"][number];

// Minimal stub for a Cognito auth session entry
const makeSession = (
  challengeResult: boolean,
  challengeName: SessionEntry["challengeName"] = "CUSTOM_CHALLENGE",
): SessionEntry => ({
  challengeName,
  challengeResult,
});

const makeEvent = (
  session: ReadonlyArray<SessionEntry>,
): DefineAuthChallengeTriggerEvent =>
  ({
    version: "1",
    region: "ca-central-1",
    userPoolId: "ca-central-1_abc123",
    userName: "testuser",
    callerContext: { awsSdkVersion: "1", clientId: "client123" },
    triggerSource: "DefineAuthChallenge_Authentication",
    request: { userAttributes: {}, session: [...session] },
    response: {
      issueTokens: false,
      failAuthentication: false,
      challengeName: "",
    },
  }) satisfies DefineAuthChallengeTriggerEvent;

const noopCallback = (): void => {
  // No-op
};

const TIME_IN_MS = 30000;
const fakeContext: Context = {
  awsRequestId: "12345678-1234-1234-1234-123456789012",
  callbackWaitsForEmptyEventLoop: false,
  done: () => {
    // No-op
  },
  fail: () => {
    // No-op
  },
  functionName: "test",
  functionVersion: "1",
  getRemainingTimeInMillis: () => TIME_IN_MS,
  invokedFunctionArn: "arn:aws:lambda:ca-central-1:123456789012:function:test",
  logGroupName: "/aws/lambda/test",
  logStreamName: "2023/01/01/[$LATEST]abcdef123456abcdef123456abcdef12",
  memoryLimitInMB: "128",
  succeed: () => {
    // No-op
  },
};

// Handler's return type includes Lambda's unused legacy `void` callback branch — narrow it away here once.
const invokeHandler = async (
  event: Readonly<DefineAuthChallengeTriggerEvent>,
): Promise<DefineAuthChallengeTriggerEvent> => {
  const result = await handler(event, fakeContext, noopCallback);

  if (typeof result !== "object") {
    throw new Error("handler unexpectedly returned void");
  }

  return result;
};

describe("define-auth-challenge handler", () => {
  it("empty session → issue=false, fail=false, challengeName=CUSTOM_CHALLENGE", async () => {
    const event = makeEvent([]);
    const result = await invokeHandler(event);
    expect(result.response.issueTokens).toBe(false);
    expect(result.response.failAuthentication).toBe(false);
    expect(result.response.challengeName).toBe("CUSTOM_CHALLENGE");
  });

  it("session length 1 with challengeResult=true → issueTokens=true, failAuthentication=false", async () => {
    const event = makeEvent([makeSession(true)]);
    const result = await invokeHandler(event);
    expect(result.response.issueTokens).toBe(true);
    expect(result.response.failAuthentication).toBe(false);
  });

  it("session length 1 with challengeResult=false → issueTokens=false, failAuthentication=true", async () => {
    const event = makeEvent([makeSession(false)]);
    const result = await invokeHandler(event);
    expect(result.response.issueTokens).toBe(false);
    expect(result.response.failAuthentication).toBe(true);
  });

  it("session length 2 with first challengeResult=true → issueTokens=false, failAuthentication=true", async () => {
    const event = makeEvent([makeSession(true), makeSession(false)]);
    const result = await invokeHandler(event);
    expect(result.response.issueTokens).toBe(false);
    expect(result.response.failAuthentication).toBe(true);
  });

  it("always returns the (modified) event", async () => {
    const event = makeEvent([]);
    const result = await invokeHandler(event);
    expect(result).toBe(event);
  });

  it("session length 2 both true → fail=true (only length=1 passes)", async () => {
    const event = makeEvent([makeSession(true), makeSession(true)]);
    const result = await invokeHandler(event);
    expect(result.response.failAuthentication).toBe(true);
    expect(result.response.issueTokens).toBe(false);
  });
});

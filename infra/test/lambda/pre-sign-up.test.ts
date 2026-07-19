import type { Context, PreSignUpTriggerEvent } from "aws-lambda";
import { handler } from "../../lambda/auth/pre-sign-up";

const makeEvent = (): PreSignUpTriggerEvent =>
  ({
    version: "1",
    region: "ca-central-1",
    userPoolId: "ca-central-1_abc123",
    userName: "testuser",
    callerContext: { awsSdkVersion: "1", clientId: "client123" },
    triggerSource: "PreSignUp_SignUp",
    request: {
      userAttributes: { email: "test@example.com" },
      validationData: { empty: "" },
    },
    response: {
      autoConfirmUser: false,
      autoVerifyEmail: false,
      autoVerifyPhone: false,
    },
  }) satisfies PreSignUpTriggerEvent;

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
  event: Readonly<PreSignUpTriggerEvent>,
): Promise<PreSignUpTriggerEvent> => {
  const result = await handler(event, fakeContext, noopCallback);

  if (typeof result !== "object") {
    throw new Error("handler unexpectedly returned void");
  }

  return result;
};

describe("pre-sign-up handler", () => {
  it("sets autoConfirmUser to true", async () => {
    const result = await invokeHandler(makeEvent());
    expect(result.response.autoConfirmUser).toBe(true);
  });

  it("sets autoVerifyEmail to true", async () => {
    const result = await invokeHandler(makeEvent());
    expect(result.response.autoVerifyEmail).toBe(true);
  });

  it("returns the event object", async () => {
    const event = makeEvent();
    const result = await handler(event, fakeContext, noopCallback);
    expect(result).toBe(event);
  });

  it("does not throw for any user attributes", async () => {
    const event = makeEvent();
    event.request.userAttributes = {
      email: "another@example.com",
      name: "Test User",
    };
    await expect(
      handler(event, fakeContext, noopCallback),
    ).resolves.not.toThrow();
  });
});

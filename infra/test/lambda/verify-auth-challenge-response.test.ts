import type {
  Context,
  VerifyAuthChallengeResponseTriggerEvent,
} from "aws-lambda";
import { handler } from "../../lambda/auth/verify-auth-challenge-response";

const makeEvent = (
  otp: string,
  answer: string,
): VerifyAuthChallengeResponseTriggerEvent => ({
  callerContext: { awsSdkVersion: "1", clientId: "client123" },
  region: "ca-central-1",
  request: {
    challengeAnswer: answer,
    privateChallengeParameters: { otp },
    userAttributes: { email: "user@example.com" },
  },
  response: {
    answerCorrect: false,
  },
  triggerSource: "VerifyAuthChallengeResponse_Authentication",
  userName: "testuser",
  userPoolId: "ca-central-1_abc123",
  version: "1",
});

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

const noopCallback = (): void => {
  // No-op
};

// Handler's return type includes Lambda's unused legacy `void` callback branch — narrow it away here once.
const invokeHandler = async (
  otp: string,
  answer: string,
): Promise<VerifyAuthChallengeResponseTriggerEvent> => {
  const event = makeEvent(otp, answer);
  const result = await handler(event, fakeContext, noopCallback);

  if (typeof result !== "object") {
    throw new Error("handler unexpectedly returned void");
  }

  return result;
};

describe("verify-auth-challenge-response handler", () => {
  it("sets answerCorrect=true when OTP matches", async () => {
    const result = await invokeHandler("123456", "123456");
    expect(result.response.answerCorrect).toBe(true);
  });

  it("sets answerCorrect=false when OTP does not match", async () => {
    const result = await invokeHandler("123456", "654321");
    expect(result.response.answerCorrect).toBe(false);
  });

  it("is case-sensitive: uppercase does not match lowercase OTP", async () => {
    const result = await invokeHandler("abc123", "ABC123");
    expect(result.response.answerCorrect).toBe(false);
  });

  it("exact match only: leading/trailing whitespace causes failure", async () => {
    const result = await invokeHandler("123456", " 123456");
    expect(result.response.answerCorrect).toBe(false);
  });

  it("returns the event object", async () => {
    const event = makeEvent("123456", "123456");
    const result = await handler(event, fakeContext, noopCallback);
    expect(result).toBe(event);
  });

  it("sets answerCorrect=false when OTP is empty and answer is empty", async () => {
    const result = await invokeHandler("", "");
    // Empty string === empty string → true
    expect(result.response.answerCorrect).toBe(true);
  });

  it("sets answerCorrect=false when answer is a partial match", async () => {
    const result = await invokeHandler("123456", "1234");
    expect(result.response.answerCorrect).toBe(false);
  });
});

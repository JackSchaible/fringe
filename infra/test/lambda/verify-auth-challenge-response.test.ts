import { handler } from "../../lambda/auth/verify-auth-challenge-response";
import type {
  VerifyAuthChallengeResponseTriggerEvent,
  Context,
} from "aws-lambda";

function makeEvent(
  otp: string,
  answer: string,
): VerifyAuthChallengeResponseTriggerEvent {
  return {
    version: "1",
    region: "ca-central-1",
    userPoolId: "ca-central-1_abc123",
    userName: "testuser",
    callerContext: { awsSdkVersion: "1", clientId: "client123" },
    triggerSource: "VerifyAuthChallengeResponse_Authentication",
    request: {
      userAttributes: { email: "user@example.com" },
      privateChallengeParameters: { otp },
      challengeAnswer: answer,
    },
    response: {
      answerCorrect: false,
    },
  } as unknown as VerifyAuthChallengeResponseTriggerEvent;
}

const fakeContext = {} as Context;

describe("verify-auth-challenge-response handler", () => {
  it("sets answerCorrect=true when OTP matches", async () => {
    const event = makeEvent("123456", "123456");
    const result = await handler(event, fakeContext, () => {});
    expect(result!.response.answerCorrect).toBe(true);
  });

  it("sets answerCorrect=false when OTP does not match", async () => {
    const event = makeEvent("123456", "654321");
    const result = await handler(event, fakeContext, () => {});
    expect(result!.response.answerCorrect).toBe(false);
  });

  it("is case-sensitive: uppercase does not match lowercase OTP", async () => {
    const event = makeEvent("abc123", "ABC123");
    const result = await handler(event, fakeContext, () => {});
    expect(result!.response.answerCorrect).toBe(false);
  });

  it("exact match only: leading/trailing whitespace causes failure", async () => {
    const event = makeEvent("123456", " 123456");
    const result = await handler(event, fakeContext, () => {});
    expect(result!.response.answerCorrect).toBe(false);
  });

  it("returns the event object", async () => {
    const event = makeEvent("123456", "123456");
    const result = await handler(event, fakeContext, () => {});
    expect(result).toBe(event);
  });

  it("sets answerCorrect=false when OTP is empty and answer is empty", async () => {
    const event = makeEvent("", "");
    const result = await handler(event, fakeContext, () => {});
    // Empty string === empty string → true
    expect(result!.response.answerCorrect).toBe(true);
  });

  it("sets answerCorrect=false when answer is a partial match", async () => {
    const event = makeEvent("123456", "1234");
    const result = await handler(event, fakeContext, () => {});
    expect(result!.response.answerCorrect).toBe(false);
  });
});

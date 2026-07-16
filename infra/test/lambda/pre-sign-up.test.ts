import { handler } from "../../lambda/auth/pre-sign-up";
import type { PreSignUpTriggerEvent, Context } from "aws-lambda";

function makeEvent(): PreSignUpTriggerEvent {
  return {
    version: "1",
    region: "ca-central-1",
    userPoolId: "ca-central-1_abc123",
    userName: "testuser",
    callerContext: { awsSdkVersion: "1", clientId: "client123" },
    triggerSource: "PreSignUp_SignUp",
    request: {
      userAttributes: { email: "test@example.com" },
      validationData: null as unknown as Record<string, string>,
    },
    response: {
      autoConfirmUser: false,
      autoVerifyEmail: false,
      autoVerifyPhone: false,
    },
  } as unknown as PreSignUpTriggerEvent;
}

const fakeContext = {} as Context;

describe("pre-sign-up handler", () => {
  it("sets autoConfirmUser to true", async () => {
    const event = makeEvent();
    const result = await handler(event, fakeContext, () => {});
    expect(result!.response.autoConfirmUser).toBe(true);
  });

  it("sets autoVerifyEmail to true", async () => {
    const event = makeEvent();
    const result = await handler(event, fakeContext, () => {});
    expect(result!.response.autoVerifyEmail).toBe(true);
  });

  it("returns the event object", async () => {
    const event = makeEvent();
    const result = await handler(event, fakeContext, () => {});
    expect(result).toBe(event);
  });

  it("does not throw for any user attributes", async () => {
    const event = makeEvent();
    event.request.userAttributes = {
      email: "another@example.com",
      name: "Test User",
    };
    await expect(handler(event, fakeContext, () => {})).resolves.not.toThrow();
  });
});

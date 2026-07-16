import { handler } from "../../lambda/auth/define-auth-challenge";
import type { DefineAuthChallengeTriggerEvent, Context } from "aws-lambda";

// Minimal stub for a Cognito auth session entry
function makeSession(
  challengeResult: boolean,
  challengeName = "CUSTOM_CHALLENGE",
) {
  return {
    challengeName,
    challengeResult,
    challengeMetadata: undefined as unknown as string,
    clientMetadata: undefined,
  };
}

function makeEvent(
  session: ReturnType<typeof makeSession>[],
): DefineAuthChallengeTriggerEvent {
  return {
    version: "1",
    region: "ca-central-1",
    userPoolId: "ca-central-1_abc123",
    userName: "testuser",
    callerContext: { awsSdkVersion: "1", clientId: "client123" },
    triggerSource: "DefineAuthChallenge_Authentication",
    request: { userAttributes: {}, session },
    response: {
      issueTokens: false,
      failAuthentication: false,
      challengeName: "",
    },
  } as unknown as DefineAuthChallengeTriggerEvent;
}

const fakeContext = {} as Context;

describe("define-auth-challenge handler", () => {
  it("empty session → issue=false, fail=false, challengeName=CUSTOM_CHALLENGE", async () => {
    const event = makeEvent([]);
    const result = await handler(event, fakeContext, () => {});
    expect(result!.response.issueTokens).toBe(false);
    expect(result!.response.failAuthentication).toBe(false);
    expect(result!.response.challengeName).toBe("CUSTOM_CHALLENGE");
  });

  it("session length 1 with challengeResult=true → issueTokens=true, failAuthentication=false", async () => {
    const event = makeEvent([makeSession(true)]);
    const result = await handler(event, fakeContext, () => {});
    expect(result!.response.issueTokens).toBe(true);
    expect(result!.response.failAuthentication).toBe(false);
  });

  it("session length 1 with challengeResult=false → issueTokens=false, failAuthentication=true", async () => {
    const event = makeEvent([makeSession(false)]);
    const result = await handler(event, fakeContext, () => {});
    expect(result!.response.issueTokens).toBe(false);
    expect(result!.response.failAuthentication).toBe(true);
  });

  it("session length 2 with first challengeResult=true → issueTokens=false, failAuthentication=true", async () => {
    const event = makeEvent([makeSession(true), makeSession(false)]);
    const result = await handler(event, fakeContext, () => {});
    expect(result!.response.issueTokens).toBe(false);
    expect(result!.response.failAuthentication).toBe(true);
  });

  it("always returns the (modified) event", async () => {
    const event = makeEvent([]);
    const result = await handler(event, fakeContext, () => {});
    expect(result).toBe(event);
  });

  it("session length 2 both true → fail=true (only length=1 passes)", async () => {
    const event = makeEvent([makeSession(true), makeSession(true)]);
    const result = await handler(event, fakeContext, () => {});
    expect(result!.response.failAuthentication).toBe(true);
    expect(result!.response.issueTokens).toBe(false);
  });
});

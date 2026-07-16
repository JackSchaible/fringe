import type { CreateAuthChallengeTriggerEvent, Context } from "aws-lambda";

// Mock the AWS SDK SES client before importing the handler
jest.mock("@aws-sdk/client-ses", () => {
  const mockSend = jest.fn().mockResolvedValue({});
  return {
    SESClient: jest.fn().mockImplementation(() => ({ send: mockSend })),
    SendEmailCommand: jest.fn().mockImplementation((input) => ({ input })),
    __mockSend: mockSend,
  };
});

// Import handler AFTER mocking
import { handler } from "../../lambda/auth/create-auth-challenge";

const { __mockSend } = jest.requireMock("@aws-sdk/client-ses") as {
  __mockSend: jest.Mock;
  SESClient: jest.Mock;
  SendEmailCommand: jest.Mock;
};
const { SendEmailCommand } = jest.requireMock("@aws-sdk/client-ses") as {
  SendEmailCommand: jest.Mock;
  SESClient: jest.Mock;
  __mockSend: jest.Mock;
};

function makeEvent(
  email = "user@example.com",
): CreateAuthChallengeTriggerEvent {
  return {
    version: "1",
    region: "ca-central-1",
    userPoolId: "ca-central-1_abc123",
    userName: "testuser",
    callerContext: { awsSdkVersion: "1", clientId: "client123" },
    triggerSource: "CreateAuthChallenge_Authentication",
    request: {
      userAttributes: { email },
      challengeName: "CUSTOM_CHALLENGE",
      session: [],
    },
    response: {
      publicChallengeParameters: {},
      privateChallengeParameters: {},
      challengeMetadata: "",
    },
  } as unknown as CreateAuthChallengeTriggerEvent;
}

const fakeContext = {} as Context;

describe("create-auth-challenge handler", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    process.env["FROM_EMAIL"] = "info@fringe.jackschaible.ca";
  });

  it("generates a 6-digit OTP stored in privateChallengeParameters", async () => {
    const event = makeEvent();
    const result = await handler(event, fakeContext, () => {});
    const otp = result!.response.privateChallengeParameters["otp"];
    expect(otp).toMatch(/^\d{6}$/);
  });

  it("OTP is between 100000 and 999999 (inclusive)", async () => {
    const event = makeEvent();
    const result = await handler(event, fakeContext, () => {});
    const otp = parseInt(
      result!.response.privateChallengeParameters["otp"],
      10,
    );
    expect(otp).toBeGreaterThanOrEqual(100000);
    expect(otp).toBeLessThanOrEqual(999999);
  });

  it("sends SES email to the user email address", async () => {
    const event = makeEvent("recipient@example.com");
    await handler(event, fakeContext, () => {});
    expect(__mockSend).toHaveBeenCalledTimes(1);
    const commandArg = SendEmailCommand.mock.calls[0][0];
    expect(commandArg.Destination.ToAddresses).toContain(
      "recipient@example.com",
    );
  });

  it("sends SES email from FROM_EMAIL env variable", async () => {
    process.env["FROM_EMAIL"] = "custom@fringe.jackschaible.ca";
    const event = makeEvent();
    await handler(event, fakeContext, () => {});
    const commandArg = SendEmailCommand.mock.calls[0][0];
    expect(commandArg.Source).toBe("custom@fringe.jackschaible.ca");
  });

  it('sets email subject to "Your Fringe sign-in code"', async () => {
    const event = makeEvent();
    await handler(event, fakeContext, () => {});
    const commandArg = SendEmailCommand.mock.calls[0][0];
    expect(commandArg.Message.Subject.Data).toBe("Your Fringe sign-in code");
  });

  it("includes the OTP in the email body text", async () => {
    const event = makeEvent();
    const result = await handler(event, fakeContext, () => {});
    const otp = result!.response.privateChallengeParameters["otp"];
    const commandArg = SendEmailCommand.mock.calls[0][0];
    expect(commandArg.Message.Body.Text.Data).toContain(otp);
  });

  it("sets publicChallengeParameters.email to the user email", async () => {
    const event = makeEvent("user@example.com");
    const result = await handler(event, fakeContext, () => {});
    expect(result!.response.publicChallengeParameters["email"]).toBe(
      "user@example.com",
    );
  });

  it("sets challengeMetadata to OTP", async () => {
    const event = makeEvent();
    const result = await handler(event, fakeContext, () => {});
    expect(result!.response.challengeMetadata).toBe("OTP");
  });

  it("returns the event object", async () => {
    const event = makeEvent();
    const result = await handler(event, fakeContext, () => {});
    expect(result).toBe(event);
  });

  it("throws when SES send fails", async () => {
    __mockSend.mockRejectedValueOnce(new Error("SES error"));
    const event = makeEvent();
    await expect(handler(event, fakeContext, () => {})).rejects.toThrow(
      "SES error",
    );
  });
});

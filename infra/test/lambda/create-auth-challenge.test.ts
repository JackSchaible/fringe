import type { Context, CreateAuthChallengeTriggerEvent } from "aws-lambda";
import type { SendEmailCommandInput } from "@aws-sdk/client-ses";

/*
 * Mock the AWS SDK SES client before importing the handler. Jest hoists
 * jest.mock() above these declarations, but its hoist-checker allows
 * referencing out-of-scope identifiers prefixed with "mock".
 */
const mockSend = jest.fn().mockResolvedValue({});
const mockSendEmailCommand = jest.fn(
  (input: Readonly<SendEmailCommandInput>) => ({
    input,
  }),
);

jest.mock("@aws-sdk/client-ses", () => ({
  SESClient: jest.fn().mockImplementation(() => ({ send: mockSend })),
  SendEmailCommand: mockSendEmailCommand,
}));

// Import handler AFTER mocking
import { handler } from "../../lambda/auth/create-auth-challenge";

const makeEvent = (
  email = "user@example.com",
): CreateAuthChallengeTriggerEvent =>
  ({
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
  }) satisfies CreateAuthChallengeTriggerEvent;

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
  event: Readonly<CreateAuthChallengeTriggerEvent>,
): Promise<CreateAuthChallengeTriggerEvent> => {
  const result = await handler(event, fakeContext, noopCallback);

  if (typeof result !== "object") {
    throw new Error("handler unexpectedly returned void");
  }

  return result;
};

describe("create-auth-challenge handler", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    process.env.FROM_EMAIL = "info@fringequest.app";
  });

  it("generates a 6-digit OTP stored in privateChallengeParameters", async () => {
    const event = makeEvent();
    const result = await invokeHandler(event);
    const { otp } = result.response.privateChallengeParameters;
    expect(otp).toMatch(/^\d{6}$/u);
  });

  it("OTP is between 100000 and 999999 (inclusive)", async () => {
    const event = makeEvent();
    const result = await invokeHandler(event);
    const otp = parseInt(result.response.privateChallengeParameters.otp, 10);
    expect(otp).toBeGreaterThanOrEqual(100000);
    expect(otp).toBeLessThanOrEqual(999999);
  });

  it("sends SES email to the user email address", async () => {
    const event = makeEvent("recipient@example.com");
    await invokeHandler(event);
    expect(mockSend).toHaveBeenCalledTimes(1);
    const [[commandArg]] = mockSendEmailCommand.mock.calls;
    expect(commandArg.Destination?.ToAddresses).toContain(
      "recipient@example.com",
    );
  });

  it("sends SES email from FROM_EMAIL env variable", async () => {
    process.env.FROM_EMAIL = "custom@fringequest.app";
    const event = makeEvent();
    await invokeHandler(event);
    const [[commandArg]] = mockSendEmailCommand.mock.calls;
    expect(commandArg.Source).toBe("custom@fringequest.app");
  });

  it('sets email subject to "Your Fringe sign-in code"', async () => {
    const event = makeEvent();
    await invokeHandler(event);
    const [[commandArg]] = mockSendEmailCommand.mock.calls;
    expect(commandArg.Message?.Subject?.Data).toBe("Your Fringe sign-in code");
  });

  it("includes the OTP in the email body text", async () => {
    const event = makeEvent();
    const result = await invokeHandler(event);
    const { otp } = result.response.privateChallengeParameters;
    const [[commandArg]] = mockSendEmailCommand.mock.calls;
    expect(commandArg.Message?.Body?.Text?.Data).toContain(otp);
  });

  it("sets publicChallengeParameters.email to the user email", async () => {
    const event = makeEvent("user@example.com");
    const result = await invokeHandler(event);
    expect(result.response.publicChallengeParameters.email).toBe(
      "user@example.com",
    );
  });

  it("sets challengeMetadata to OTP", async () => {
    const event = makeEvent();
    const result = await invokeHandler(event);
    expect(result.response.challengeMetadata).toBe("OTP");
  });

  it("returns the event object", async () => {
    const event = makeEvent();
    const result = await invokeHandler(event);
    expect(result).toBe(event);
  });

  it("throws when SES send fails", async () => {
    mockSend.mockRejectedValueOnce(new Error("SES error"));
    const event = makeEvent();
    await expect(invokeHandler(event)).rejects.toThrow("SES error");
  });
});

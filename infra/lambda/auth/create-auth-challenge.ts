import type {
  CreateAuthChallengeTriggerEvent,
  CreateAuthChallengeTriggerHandler,
  Handler,
} from "aws-lambda";
import { SESClient, SendEmailCommand } from "@aws-sdk/client-ses";

const ses = new SESClient({});

const OTP_LOWER_BOUND = 100_000;
const OTP_RANGE = 900_000;

const sendOtpEmail = async (toAddress: string, otp: string): Promise<void> => {
  try {
    await ses.send(
      new SendEmailCommand({
        Source: process.env.FROM_EMAIL,
        Destination: { ToAddresses: [toAddress] },
        Message: {
          Subject: { Data: "Your Fringe sign-in code" },
          Body: {
            Text: {
              Data: `Your sign-in code is: ${otp}\n\nThis code expires in 3 minutes.`,
            },
          },
        },
      }),
    );
    console.log("CreateAuthChallenge: email sent successfully");
  } catch (err) {
    console.error("CreateAuthChallenge: SES send failed:", err);
    throw err;
  }
};

export const handler: Handler<
  CreateAuthChallengeTriggerEvent,
  CreateAuthChallengeTriggerEvent
> = (async (
  event: Readonly<CreateAuthChallengeTriggerEvent>,
): Promise<CreateAuthChallengeTriggerEvent> => {
  const otp = String(Math.floor(OTP_LOWER_BOUND + Math.random() * OTP_RANGE));
  const toAddress = event.request.userAttributes.email;
  console.log("CreateAuthChallenge: sending OTP to", toAddress);

  await sendOtpEmail(toAddress, otp);

  event.response.publicChallengeParameters = { email: toAddress };
  event.response.privateChallengeParameters = { otp };
  event.response.challengeMetadata = "OTP";
  return event;
}) satisfies CreateAuthChallengeTriggerHandler;

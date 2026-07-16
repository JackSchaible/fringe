import { SESClient, SendEmailCommand } from "@aws-sdk/client-ses";
import type { CreateAuthChallengeTriggerHandler } from "aws-lambda";

const ses = new SESClient({});

export const handler: CreateAuthChallengeTriggerHandler = async (event) => {
  const otp = String(Math.floor(100000 + Math.random() * 900000));
  const toAddress = event.request.userAttributes["email"];
  console.log("CreateAuthChallenge: sending OTP to", toAddress);

  try {
    await ses.send(
      new SendEmailCommand({
        Source: process.env["FROM_EMAIL"],
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

  event.response.publicChallengeParameters = { email: toAddress };
  event.response.privateChallengeParameters = { otp };
  event.response.challengeMetadata = "OTP";
  return event;
};

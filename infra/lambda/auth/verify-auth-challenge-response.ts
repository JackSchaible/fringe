import type {
  Handler,
  VerifyAuthChallengeResponseTriggerEvent,
  VerifyAuthChallengeResponseTriggerHandler,
} from "aws-lambda";

export const handler: Handler<
  VerifyAuthChallengeResponseTriggerEvent,
  VerifyAuthChallengeResponseTriggerEvent
> = (async (
  event: Readonly<VerifyAuthChallengeResponseTriggerEvent>,
): Promise<VerifyAuthChallengeResponseTriggerEvent> => {
  const expected = event.request.privateChallengeParameters.otp;
  const provided = event.request.challengeAnswer;
  event.response.answerCorrect = provided === expected;
  console.log(
    "VerifyAuthChallengeResponse: match=",
    event.response.answerCorrect,
    "expected length=",
    expected.length,
    "provided length=",
    provided.length,
  );
  return event;
}) satisfies VerifyAuthChallengeResponseTriggerHandler;

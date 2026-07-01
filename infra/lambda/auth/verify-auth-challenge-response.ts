import type { VerifyAuthChallengeResponseTriggerHandler } from 'aws-lambda';

export const handler: VerifyAuthChallengeResponseTriggerHandler = async (event) => {
  const expected = event.request.privateChallengeParameters['otp'];
  const provided = event.request.challengeAnswer;
  event.response.answerCorrect = provided === expected;
  console.log(
    'VerifyAuthChallengeResponse: match=',
    event.response.answerCorrect,
    'expected length=',
    expected?.length,
    'provided length=',
    provided?.length,
  );
  return event;
};

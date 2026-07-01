import type { DefineAuthChallengeTriggerHandler } from 'aws-lambda';

export const handler: DefineAuthChallengeTriggerHandler = async (event) => {
  const { session } = event.request;
  console.log(
    'DefineAuthChallenge session length:',
    session.length,
    JSON.stringify(session.map((s) => ({ name: s.challengeName, result: s.challengeResult }))),
  );

  if (session.length === 0) {
    event.response.issueTokens = false;
    event.response.failAuthentication = false;
    event.response.challengeName = 'CUSTOM_CHALLENGE';
  } else if (session.length === 1 && session[0].challengeResult === true) {
    event.response.issueTokens = true;
    event.response.failAuthentication = false;
  } else {
    event.response.issueTokens = false;
    event.response.failAuthentication = true;
  }

  console.log('DefineAuthChallenge response:', JSON.stringify(event.response));
  return event;
};

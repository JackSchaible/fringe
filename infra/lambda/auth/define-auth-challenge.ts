import type {
  DefineAuthChallengeTriggerEvent,
  DefineAuthChallengeTriggerHandler,
  Handler,
} from "aws-lambda";

const NO_PRIOR_ATTEMPTS = 0;
const SINGLE_ATTEMPT = 1;
const FIRST_ATTEMPT_INDEX = 0;

type ChallengeAttempt = DefineAuthChallengeTriggerEvent["request"]["session"][number];

const resolveChallengeOutcome = (
  session: ReadonlyArray<ChallengeAttempt>,
): Pick<
  DefineAuthChallengeTriggerEvent["response"],
  "challengeName" | "failAuthentication" | "issueTokens"
> => {
  if (session.length === NO_PRIOR_ATTEMPTS) {
    return {
      issueTokens: false,
      failAuthentication: false,
      challengeName: "CUSTOM_CHALLENGE",
    };
  }

  if (
    session.length === SINGLE_ATTEMPT &&
    session[FIRST_ATTEMPT_INDEX].challengeResult
  ) {
    return { issueTokens: true, failAuthentication: false };
  }

  return { issueTokens: false, failAuthentication: true };
};

export const handler: Handler<
  DefineAuthChallengeTriggerEvent,
  DefineAuthChallengeTriggerEvent
> = (async (
  event: Readonly<DefineAuthChallengeTriggerEvent>,
): Promise<DefineAuthChallengeTriggerEvent> => {
  const { session } = event.request;
  console.log(
    "DefineAuthChallenge session length:",
    session.length,
    JSON.stringify(
      session.map((attempt) => ({
        name: attempt.challengeName,
        result: attempt.challengeResult,
      })),
    ),
  );

  Object.assign(event.response, resolveChallengeOutcome(session));

  console.log("DefineAuthChallenge response:", JSON.stringify(event.response));
  return event;
}) satisfies DefineAuthChallengeTriggerHandler;

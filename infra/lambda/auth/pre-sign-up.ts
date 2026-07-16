import type {
  Handler,
  PreSignUpTriggerEvent,
  PreSignUpTriggerHandler,
} from "aws-lambda";

export const handler: Handler<PreSignUpTriggerEvent, PreSignUpTriggerEvent> =
  (async (
    event: Readonly<PreSignUpTriggerEvent>,
  ): Promise<PreSignUpTriggerEvent> => {
    event.response.autoConfirmUser = true;
    event.response.autoVerifyEmail = true;
    return event;
  }) satisfies PreSignUpTriggerHandler;

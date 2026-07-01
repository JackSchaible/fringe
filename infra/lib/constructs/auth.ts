import * as cdk from "aws-cdk-lib";
import * as cognito from "aws-cdk-lib/aws-cognito";
import {
  Function as LambdaFunction,
  Runtime,
  Code,
} from "aws-cdk-lib/aws-lambda";
import { PolicyStatement } from "aws-cdk-lib/aws-iam";
import { EmailIdentity, Identity } from "aws-cdk-lib/aws-ses";
import { Construct } from "constructs";

export class FringeAuth extends Construct {
  public readonly userPool: cognito.UserPool;
  public readonly userPoolClient: cognito.UserPoolClient;

  constructor(scope: Construct, id: string) {
    super(scope, id);

    this.userPool = new cognito.UserPool(this, "UserPool", {
      userPoolName: "fringe-users",
      selfSignUpEnabled: true,
      signInAliases: { email: true },
      autoVerify: { email: true },
      standardAttributes: {
        email: { required: true, mutable: true },
        fullname: { required: false, mutable: true },
      },
      accountRecovery: cognito.AccountRecovery.EMAIL_ONLY,
      passwordPolicy: {
        minLength: 8,
        requireDigits: false,
        requireLowercase: false,
        requireUppercase: false,
        requireSymbols: false,
      },
      removalPolicy: cdk.RemovalPolicy.RETAIN,
    });

    // ── Pre-signup: auto-confirm so signUp → signIn works without a separate
    //    confirmation step (we verify identity via the OTP challenge instead)
    const preSignUp = new LambdaFunction(this, "PreSignUp", {
      runtime: Runtime.NODEJS_22_X,
      handler: "index.handler",
      code: Code.fromInline(`
exports.handler = async (event) => {
  event.response.autoConfirmUser = true;
  event.response.autoVerifyEmail = true;
  return event;
};
`),
    });
    this.userPool.addTrigger(cognito.UserPoolOperation.PRE_SIGN_UP, preSignUp);

    // ── Email OTP triggers ─────────────────────────────────────────────────
    const fromEmail = "info@fringe.jackschaible.ca";

    new EmailIdentity(this, "SesFromIdentity", {
      identity: Identity.domain("fringe.jackschaible.ca"),
    });

    const defineAuthChallenge = new LambdaFunction(
      this,
      "DefineAuthChallenge",
      {
        runtime: Runtime.NODEJS_22_X,
        handler: "index.handler",
        code: Code.fromInline(`
exports.handler = async (event) => {
  const { session } = event.request;
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
  return event;
};
`),
      },
    );

    const createAuthChallenge = new LambdaFunction(
      this,
      "CreateAuthChallenge",
      {
        runtime: Runtime.NODEJS_22_X,
        handler: "index.handler",
        environment: { FROM_EMAIL: fromEmail },
        code: Code.fromInline(`
const { SESClient, SendEmailCommand } = require('@aws-sdk/client-ses');
exports.handler = async (event) => {
  const otp = String(Math.floor(100000 + Math.random() * 900000));
  const ses = new SESClient({});
  await ses.send(new SendEmailCommand({
    Source: process.env.FROM_EMAIL,
    Destination: { ToAddresses: [event.request.userAttributes.email] },
    Message: {
      Subject: { Data: 'Your Fringe sign-in code' },
      Body: {
        Text: { Data: 'Your sign-in code is: ' + otp + '\\n\\nThis code expires in 3 minutes.' },
      },
    },
  }));
  event.response.publicChallengeParameters = { email: event.request.userAttributes.email };
  event.response.privateChallengeParameters = { otp };
  event.response.challengeMetadata = 'OTP';
  return event;
};
`),
      },
    );

    createAuthChallenge.addToRolePolicy(
      new PolicyStatement({
        actions: ["ses:SendEmail", "ses:SendRawEmail"],
        resources: ["*"],
      }),
    );

    const verifyAuthChallengeResponse = new LambdaFunction(
      this,
      "VerifyAuthChallengeResponse",
      {
        runtime: Runtime.NODEJS_22_X,
        handler: "index.handler",
        code: Code.fromInline(`
exports.handler = async (event) => {
  event.response.answerCorrect =
    event.request.challengeAnswer === event.request.privateChallengeParameters.otp;
  return event;
};
`),
      },
    );

    this.userPool.addTrigger(
      cognito.UserPoolOperation.DEFINE_AUTH_CHALLENGE,
      defineAuthChallenge,
    );
    this.userPool.addTrigger(
      cognito.UserPoolOperation.CREATE_AUTH_CHALLENGE,
      createAuthChallenge,
    );
    this.userPool.addTrigger(
      cognito.UserPoolOperation.VERIFY_AUTH_CHALLENGE_RESPONSE,
      verifyAuthChallengeResponse,
    );

    // ── App client ──────────────────────────────────────────────────────────

    this.userPoolClient = new cognito.UserPoolClient(this, "WebClient", {
      userPool: this.userPool,
      userPoolClientName: "fringe-spa",
      generateSecret: false,
      authFlows: {
        custom: true,
      },
      supportedIdentityProviders: [
        cognito.UserPoolClientIdentityProvider.COGNITO,
      ],
    });
  }
}

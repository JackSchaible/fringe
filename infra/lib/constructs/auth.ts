import * as cdk from "aws-cdk-lib";
import * as cognito from "aws-cdk-lib/aws-cognito";
import { Runtime } from "aws-cdk-lib/aws-lambda";
import { NodejsFunction } from "aws-cdk-lib/aws-lambda-nodejs";
import { PolicyStatement } from "aws-cdk-lib/aws-iam";
import { EmailIdentity, Identity } from "aws-cdk-lib/aws-ses";
import { Construct } from "constructs";
import * as path from "path";

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

    const lambdaDir = path.join(__dirname, "..", "..", "lambda", "auth");
    const bundling = { externalModules: ["@aws-sdk/*"] };

    const preSignUp = new NodejsFunction(this, "PreSignUp", {
      runtime: Runtime.NODEJS_22_X,
      entry: path.join(lambdaDir, "pre-sign-up.ts"),
      bundling,
    });
    this.userPool.addTrigger(cognito.UserPoolOperation.PRE_SIGN_UP, preSignUp);

    // ── Email OTP triggers ─────────────────────────────────────────────────
    const fromEmail = "info@fringe.jackschaible.ca";

    new EmailIdentity(this, "SesFromIdentity", {
      identity: Identity.domain("fringe.jackschaible.ca"),
    });

    const defineAuthChallenge = new NodejsFunction(
      this,
      "DefineAuthChallenge",
      {
        runtime: Runtime.NODEJS_22_X,
        entry: path.join(lambdaDir, "define-auth-challenge.ts"),
        bundling,
      },
    );

    const createAuthChallenge = new NodejsFunction(
      this,
      "CreateAuthChallenge",
      {
        runtime: Runtime.NODEJS_22_X,
        entry: path.join(lambdaDir, "create-auth-challenge.ts"),
        environment: { FROM_EMAIL: fromEmail },
        bundling,
      },
    );

    createAuthChallenge.addToRolePolicy(
      new PolicyStatement({
        actions: ["ses:SendEmail", "ses:SendRawEmail"],
        resources: ["*"],
      }),
    );

    const verifyAuthChallengeResponse = new NodejsFunction(
      this,
      "VerifyAuthChallengeResponse",
      {
        runtime: Runtime.NODEJS_22_X,
        entry: path.join(lambdaDir, "verify-auth-challenge-response.ts"),
        bundling,
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

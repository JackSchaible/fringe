import * as cdk from "aws-cdk-lib";
import * as cognito from "aws-cdk-lib/aws-cognito";
import * as path from "path";
import { EmailIdentity, Identity } from "aws-cdk-lib/aws-ses";
import { Construct } from "constructs";
import { NodejsFunction } from "aws-cdk-lib/aws-lambda-nodejs";
import { PolicyStatement } from "aws-cdk-lib/aws-iam";
import { Runtime } from "aws-cdk-lib/aws-lambda";

const FROM_EMAIL = "info@fringe.jackschaible.ca";

interface Bundling {
  externalModules: ReadonlyArray<string>;
}

interface TriggerContext {
  lambdaDir: string;
  bundling: Bundling;
}

export class FringeAuth extends Construct {
  public readonly userPool: cognito.UserPool;
  public readonly userPoolClient: cognito.UserPoolClient;

  public constructor(scope: Readonly<Construct>, id: string) {
    super(scope, id);

    this.userPool = this.createUserPool();

    const context: TriggerContext = {
      lambdaDir: path.join(__dirname, "..", "..", "lambda", "auth"),
      bundling: { externalModules: ["@aws-sdk/*"] },
    };

    const preSignUp = this.createTriggerFunction(
      context,
      "PreSignUp",
      "pre-sign-up.ts",
    );
    this.userPool.addTrigger(cognito.UserPoolOperation.PRE_SIGN_UP, preSignUp);

    this.setUpEmailOtpChallenge(context);

    this.userPoolClient = this.createUserPoolClient();
  }

  private createUserPool(): cognito.UserPool {
    return new cognito.UserPool(this, "UserPool", {
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
  }

  private createTriggerFunction(
    context: Readonly<TriggerContext>,
    id: string,
    entryFile: string,
  ): NodejsFunction {
    return new NodejsFunction(this, id, {
      runtime: Runtime.NODEJS_22_X,
      entry: path.join(context.lambdaDir, entryFile),
      bundling: context.bundling,
    });
  }

  private createOtpEmailFunction(
    context: Readonly<TriggerContext>,
  ): NodejsFunction {
    const createAuthChallenge = new NodejsFunction(
      this,
      "CreateAuthChallenge",
      {
        runtime: Runtime.NODEJS_22_X,
        entry: path.join(context.lambdaDir, "create-auth-challenge.ts"),
        environment: { FROM_EMAIL },
        bundling: context.bundling,
      },
    );

    createAuthChallenge.addToRolePolicy(
      new PolicyStatement({
        actions: ["ses:SendEmail", "ses:SendRawEmail"],
        resources: ["*"],
      }),
    );

    return createAuthChallenge;
  }

  private setUpEmailOtpChallenge(context: Readonly<TriggerContext>): void {
    new EmailIdentity(this, "SesFromIdentity", {
      identity: Identity.domain("fringe.jackschaible.ca"),
    });

    const defineAuthChallenge = this.createTriggerFunction(
      context,
      "DefineAuthChallenge",
      "define-auth-challenge.ts",
    );

    const createAuthChallenge = this.createOtpEmailFunction(context);

    const verifyAuthChallengeResponse = this.createTriggerFunction(
      context,
      "VerifyAuthChallengeResponse",
      "verify-auth-challenge-response.ts",
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
  }

  private createUserPoolClient(): cognito.UserPoolClient {
    return new cognito.UserPoolClient(this, "WebClient", {
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

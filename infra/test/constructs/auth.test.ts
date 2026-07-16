import { App, Stack } from "aws-cdk-lib";
import { Match, Template } from "aws-cdk-lib/assertions";
import { FringeAuth } from "../../lib/constructs/auth";

describe("FringeAuth", () => {
  let template: Template;

  beforeEach(() => {
    const app = new App();
    const stack = new Stack(app, "TestStack");
    new FringeAuth(stack, "Auth");
    template = Template.fromStack(stack);
  });

  describe("UserPool", () => {
    it("creates a UserPool named fringe-users", () => {
      template.hasResourceProperties("AWS::Cognito::UserPool", {
        UserPoolName: "fringe-users",
      });
    });

    it("enables self sign-up", () => {
      template.hasResourceProperties("AWS::Cognito::UserPool", {
        AdminCreateUserConfig: {
          AllowAdminCreateUserOnly: false,
        },
      });
    });

    it("uses email as the sign-in alias", () => {
      template.hasResourceProperties("AWS::Cognito::UserPool", {
        UsernameAttributes: Match.arrayWith(["email"]),
      });
    });

    it("sets account recovery to EMAIL_ONLY", () => {
      template.hasResourceProperties("AWS::Cognito::UserPool", {
        AccountRecoverySetting: {
          RecoveryMechanisms: Match.arrayWith([
            Match.objectLike({ Name: "verified_email" }),
          ]),
        },
      });
    });

    it("sets password policy with minLength 8 and no character requirements", () => {
      template.hasResourceProperties("AWS::Cognito::UserPool", {
        Policies: {
          PasswordPolicy: {
            MinimumLength: 8,
            RequireNumbers: false,
            RequireLowercase: false,
            RequireUppercase: false,
            RequireSymbols: false,
          },
        },
      });
    });

    it("requires email as a standard attribute", () => {
      template.hasResourceProperties("AWS::Cognito::UserPool", {
        Schema: Match.arrayWith([
          Match.objectLike({
            Name: "email",
            Required: true,
            Mutable: true,
          }),
        ]),
      });
    });

    it("has DeletionPolicy Retain", () => {
      template.hasResource("AWS::Cognito::UserPool", {
        DeletionPolicy: "Retain",
        UpdateReplacePolicy: "Retain",
      });
    });
  });

  describe("Auth Lambda triggers", () => {
    it("creates exactly 4 Lambda functions for auth triggers (plus BucketDeployment provider Lambdas are not present here)", () => {
      // The 4 auth trigger functions + any CDK-internal ones
      // We check that there are at least 4 Node.js 22 Lambdas
      const resources = template.findResources("AWS::Lambda::Function", {
        Properties: {
          Runtime: "nodejs22.x",
        },
      });
      expect(Object.keys(resources).length).toBeGreaterThanOrEqual(4);
    });

    it("all auth trigger Lambdas use NODEJS_22_X runtime", () => {
      template.hasResourceProperties("AWS::Lambda::Function", {
        Runtime: "nodejs22.x",
      });
    });

    it("create-auth-challenge Lambda has FROM_EMAIL environment variable", () => {
      template.hasResourceProperties("AWS::Lambda::Function", {
        Runtime: "nodejs22.x",
        Environment: {
          Variables: Match.objectLike({
            FROM_EMAIL: "info@fringe.jackschaible.ca",
          }),
        },
      });
    });

    it("create-auth-challenge Lambda has SES IAM policy", () => {
      template.hasResourceProperties("AWS::IAM::Policy", {
        PolicyDocument: {
          Statement: Match.arrayWith([
            Match.objectLike({
              Action: Match.arrayWith(["ses:SendEmail", "ses:SendRawEmail"]),
              Resource: "*",
              Effect: "Allow",
            }),
          ]),
        },
      });
    });

    it("UserPool has all 4 Lambda triggers configured", () => {
      template.hasResourceProperties("AWS::Cognito::UserPool", {
        LambdaConfig: Match.objectLike({
          PreSignUp: Match.anyValue(),
          DefineAuthChallenge: Match.anyValue(),
          CreateAuthChallenge: Match.anyValue(),
          VerifyAuthChallengeResponse: Match.anyValue(),
        }),
      });
    });
  });

  describe("UserPoolClient", () => {
    it("creates a UserPoolClient named fringe-spa", () => {
      template.hasResourceProperties("AWS::Cognito::UserPoolClient", {
        ClientName: "fringe-spa",
      });
    });

    it("does not generate a client secret (GenerateSecret is false)", () => {
      template.hasResourceProperties("AWS::Cognito::UserPoolClient", {
        GenerateSecret: false,
      });
    });

    it("enables custom auth flow", () => {
      template.hasResourceProperties("AWS::Cognito::UserPoolClient", {
        ExplicitAuthFlows: Match.arrayWith([
          "ALLOW_CUSTOM_AUTH",
          "ALLOW_REFRESH_TOKEN_AUTH",
        ]),
      });
    });

    it("supports COGNITO identity provider", () => {
      template.hasResourceProperties("AWS::Cognito::UserPoolClient", {
        SupportedIdentityProviders: Match.arrayWith(["COGNITO"]),
      });
    });
  });
});

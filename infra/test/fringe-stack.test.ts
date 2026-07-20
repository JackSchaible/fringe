import { App, Stack } from "aws-cdk-lib";
import {
  Certificate,
  CertificateValidation,
} from "aws-cdk-lib/aws-certificatemanager";
import { Match, Template } from "aws-cdk-lib/assertions";
import { FringeStack } from "../lib/fringe-stack";

describe("FringeStack", () => {
  let template: Template;
  let stack: FringeStack;

  beforeEach(() => {
    const app = new App();

    // CertStack would normally live in us-east-1; for the test we provide a stub certificate
    const certStack = new Stack(app, "CertStack", {
      env: { account: "123456789012", region: "us-east-1" },
      crossRegionReferences: true,
    });
    const certificate = new Certificate(certStack, "Cert", {
      domainName: "fringe.jackschaible.ca",
      validation: CertificateValidation.fromDns(),
    });

    stack = new FringeStack(app, "FringeStack", {
      env: { account: "123456789012", region: "ca-central-1" },
      crossRegionReferences: true,
      certificate,
    });

    template = Template.fromStack(stack);
  });

  describe("constructs", () => {
    it("creates a DynamoDB table (FringeDynamo)", () => {
      template.resourceCountIs("AWS::DynamoDB::GlobalTable", 1);
    });

    it("creates a Cognito UserPool (FringeAuth)", () => {
      template.hasResourceProperties("AWS::Cognito::UserPool", {
        UserPoolName: "fringe-users",
      });
    });

    it("creates a CloudFront Distribution (FringeFrontend)", () => {
      template.resourceCountIs("AWS::CloudFront::Distribution", 1);
    });

    it("creates an API Gateway REST API (FringeApi)", () => {
      template.resourceCountIs("AWS::ApiGateway::RestApi", 1);
    });

    it("creates an EventBridge Scheduler per scheduled Lambda (FringeScraper, FringeTransferMatrix)", () => {
      template.resourceCountIs("AWS::Scheduler::Schedule", 2);
    });
  });

  describe("stack properties", () => {
    it("has termination protection enabled", () => {
      expect(stack.terminationProtection).toBe(true);
    });

    it("applies project=fringe-app tag to stack resources", () => {
      /*
       * CDK stack-level tags propagate to taggable resources such as Lambda
       * functions. DynamoDB GlobalTable is not always taggable via CFN tag
       * propagation; verify the tag is present on an IAM Role, which
       * reliably receives it.
       */
      template.hasResourceProperties("AWS::IAM::Role", {
        Tags: Match.arrayWith([
          Match.objectLike({ Key: "project", Value: "fringe-app" }),
        ]),
      });
    });
  });

  describe("outputs", () => {
    it("outputs FrontendUrl with value https://fringe.jackschaible.ca", () => {
      template.hasOutput("FrontendUrl", {
        Value: "https://fringe.jackschaible.ca",
      });
    });

    it("outputs CloudFrontDomain", () => {
      template.hasOutput("CloudFrontDomain", {});
    });

    it("outputs ApiUrl with value https://api.fringe.jackschaible.ca", () => {
      template.hasOutput("ApiUrl", {
        Value: "https://api.fringe.jackschaible.ca",
      });
    });

    it("outputs ApiGatewayDomain", () => {
      template.hasOutput("ApiGatewayDomain", {});
    });

    it("outputs CognitoUserPoolId", () => {
      template.hasOutput("CognitoUserPoolId", {});
    });

    it("outputs CognitoClientId", () => {
      template.hasOutput("CognitoClientId", {});
    });
  });
});

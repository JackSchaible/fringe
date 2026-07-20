import { App, Stack } from "aws-cdk-lib";
import {
  Certificate,
  CertificateValidation,
} from "aws-cdk-lib/aws-certificatemanager";
import { Match, Template } from "aws-cdk-lib/assertions";
import { FringeApi } from "../../lib/constructs/api";
import { FringeAuth } from "../../lib/constructs/auth";
import { FringeDynamo } from "../../lib/constructs/dynamo";
import { HostedZone } from "aws-cdk-lib/aws-route53";

describe("FringeApi", () => {
  let template: Template;

  beforeEach(() => {
    const app = new App();
    const stack = new Stack(app, "TestStack", {
      env: { account: "123456789012", region: "us-east-1" },
    });
    const dynamo = new FringeDynamo(stack, "Dynamo");
    const auth = new FringeAuth(stack, "Auth");
    const hostedZone = new HostedZone(stack, "Zone", {
      zoneName: "fringequest.app",
    });
    const cert = new Certificate(stack, "Cert", {
      domainName: "fringequest.app",
      validation: CertificateValidation.fromDns(hostedZone),
    });
    new FringeApi(stack, "Api", {
      table: dynamo.table,
      certificate: cert,
      auth,
      hostedZone,
    });
    template = Template.fromStack(stack);
  });

  describe("Lambda function", () => {
    it("creates a Lambda function with DOTNET_10 runtime", () => {
      template.hasResourceProperties("AWS::Lambda::Function", {
        Runtime: "dotnet10",
      });
    });

    it("uses handler Fringe.API", () => {
      template.hasResourceProperties("AWS::Lambda::Function", {
        Runtime: "dotnet10",
        Handler: "Fringe.API",
      });
    });

    it("has a 30 second timeout", () => {
      template.hasResourceProperties("AWS::Lambda::Function", {
        Runtime: "dotnet10",
        Timeout: 30,
      });
    });

    it("has 512MB memory", () => {
      template.hasResourceProperties("AWS::Lambda::Function", {
        Runtime: "dotnet10",
        MemorySize: 512,
      });
    });

    it("sets DYNAMO_TABLE_NAME environment variable", () => {
      template.hasResourceProperties("AWS::Lambda::Function", {
        Runtime: "dotnet10",
        Environment: {
          Variables: Match.objectLike({
            DYNAMO_TABLE_NAME: Match.anyValue(),
          }),
        },
      });
    });

    it("sets ALLOWED_ORIGINS environment variable", () => {
      template.hasResourceProperties("AWS::Lambda::Function", {
        Runtime: "dotnet10",
        Environment: {
          Variables: Match.objectLike({
            ALLOWED_ORIGINS: Match.anyValue(),
          }),
        },
      });
    });

    it("sets COGNITO_USER_POOL_ID environment variable", () => {
      template.hasResourceProperties("AWS::Lambda::Function", {
        Runtime: "dotnet10",
        Environment: {
          Variables: Match.objectLike({
            COGNITO_USER_POOL_ID: Match.anyValue(),
          }),
        },
      });
    });

    it("sets TURNSTILE_SECRET_KEY environment variable", () => {
      template.hasResourceProperties("AWS::Lambda::Function", {
        Runtime: "dotnet10",
        Environment: {
          Variables: Match.objectLike({
            TURNSTILE_SECRET_KEY: Match.anyValue(),
          }),
        },
      });
    });

    it("has read/write DynamoDB IAM policy", () => {
      template.hasResourceProperties("AWS::IAM::Policy", {
        PolicyDocument: {
          Statement: Match.arrayWith([
            Match.objectLike({
              Action: Match.arrayWith([
                "dynamodb:BatchGetItem",
                "dynamodb:Query",
                "dynamodb:GetItem",
                "dynamodb:Scan",
                "dynamodb:ConditionCheckItem",
                "dynamodb:BatchWriteItem",
                "dynamodb:PutItem",
                "dynamodb:UpdateItem",
                "dynamodb:DeleteItem",
                "dynamodb:DescribeTable",
              ]),
              Effect: "Allow",
            }),
          ]),
        },
      });
    });

    it("has cognito-idp:AdminDeleteUser IAM policy", () => {
      template.hasResourceProperties("AWS::IAM::Policy", {
        PolicyDocument: {
          Statement: Match.arrayWith([
            Match.objectLike({
              Action: "cognito-idp:AdminDeleteUser",
              Effect: "Allow",
            }),
          ]),
        },
      });
    });
  });

  describe("API Gateway", () => {
    it("creates a REST API (LambdaRestApi)", () => {
      template.resourceCountIs("AWS::ApiGateway::RestApi", 1);
    });

    it("creates an API Gateway custom domain for api.fringequest.app", () => {
      template.hasResourceProperties("AWS::ApiGateway::DomainName", {
        DomainName: "api.fringequest.app",
      });
    });

    it("uses TLS 1.2 security policy", () => {
      template.hasResourceProperties("AWS::ApiGateway::DomainName", {
        DomainName: "api.fringequest.app",
        SecurityPolicy: "TLS_1_2",
      });
    });

    it("uses EDGE endpoint type", () => {
      template.hasResourceProperties("AWS::ApiGateway::DomainName", {
        DomainName: "api.fringequest.app",
        EndpointConfiguration: {
          Types: ["EDGE"],
        },
      });
    });
  });

  describe("Route53 alias record", () => {
    it("creates an A record for api.fringequest.app aliasing to API Gateway", () => {
      template.hasResourceProperties("AWS::Route53::RecordSet", {
        Name: "api.fringequest.app.",
        Type: "A",
        AliasTarget: Match.objectLike({
          DNSName: Match.anyValue(),
        }),
      });
    });
  });
});

import { App, Stack } from "aws-cdk-lib";
import { Match, Template } from "aws-cdk-lib/assertions";
import { FringeDynamo } from "../../lib/constructs/dynamo";
import { FringeScraper } from "../../lib/constructs/scraper";

describe("FringeScraper", () => {
  let template: Template;

  beforeEach(() => {
    const app = new App();
    const stack = new Stack(app, "TestStack");
    const dynamo = new FringeDynamo(stack, "Dynamo");
    new FringeScraper(stack, "Scraper", { table: dynamo.table });
    template = Template.fromStack(stack);
  });

  describe("Lambda function", () => {
    it("creates a Lambda function with DOTNET_10 runtime", () => {
      template.hasResourceProperties("AWS::Lambda::Function", {
        Runtime: "dotnet10",
      });
    });

    it("uses the correct handler", () => {
      template.hasResourceProperties("AWS::Lambda::Function", {
        Runtime: "dotnet10",
        Handler: "FringeScraper::FringeScraper.LambdaHandler::FunctionHandler",
      });
    });

    it("has a 5 minute (300 second) timeout", () => {
      template.hasResourceProperties("AWS::Lambda::Function", {
        Runtime: "dotnet10",
        Timeout: 300,
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

    it("sets OPENROUTESERVICE_API_KEY environment variable", () => {
      template.hasResourceProperties("AWS::Lambda::Function", {
        Runtime: "dotnet10",
        Environment: {
          Variables: Match.objectLike({
            OPENROUTESERVICE_API_KEY: Match.anyValue(),
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
  });

  describe("EventBridge Scheduler", () => {
    it("creates an EventBridge Scheduler resource", () => {
      template.resourceCountIs("AWS::Scheduler::Schedule", 1);
    });

    it("uses cron(0 3 * * ? *) schedule expression", () => {
      template.hasResourceProperties("AWS::Scheduler::Schedule", {
        ScheduleExpression: "cron(0 3 * * ? *)",
      });
    });

    it("uses America/Edmonton timezone", () => {
      template.hasResourceProperties("AWS::Scheduler::Schedule", {
        ScheduleExpressionTimezone: "America/Edmonton",
      });
    });

    it("uses flexible time window mode OFF", () => {
      template.hasResourceProperties("AWS::Scheduler::Schedule", {
        FlexibleTimeWindow: {
          Mode: "OFF",
        },
      });
    });

    it("creates IAM Role for the scheduler assumed by scheduler.amazonaws.com", () => {
      template.hasResourceProperties("AWS::IAM::Role", {
        AssumeRolePolicyDocument: {
          Statement: Match.arrayWith([
            Match.objectLike({
              Action: "sts:AssumeRole",
              Effect: "Allow",
              Principal: {
                Service: "scheduler.amazonaws.com",
              },
            }),
          ]),
        },
      });
    });

    it("scheduler role has invoke permission on the Lambda", () => {
      template.hasResourceProperties("AWS::IAM::Policy", {
        PolicyDocument: {
          Statement: Match.arrayWith([
            Match.objectLike({
              Action: "lambda:InvokeFunction",
              Effect: "Allow",
            }),
          ]),
        },
      });
    });
  });
});

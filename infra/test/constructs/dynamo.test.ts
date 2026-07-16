import { App, Stack } from "aws-cdk-lib";
import { Template, Match } from "aws-cdk-lib/assertions";
import { FringeDynamo } from "../../lib/constructs/dynamo";

describe("FringeDynamo", () => {
  let template: Template;

  beforeEach(() => {
    const app = new App();
    const stack = new Stack(app, "TestStack");
    new FringeDynamo(stack, "Dynamo");
    template = Template.fromStack(stack);
  });

  it("creates a DynamoDB GlobalTable named fringe", () => {
    template.hasResourceProperties("AWS::DynamoDB::GlobalTable", {
      TableName: "fringe",
    });
  });

  it("has partition key pk of type STRING", () => {
    template.hasResourceProperties("AWS::DynamoDB::GlobalTable", {
      AttributeDefinitions: Match.arrayWith([
        { AttributeName: "pk", AttributeType: "S" },
      ]),
      KeySchema: Match.arrayWith([{ AttributeName: "pk", KeyType: "HASH" }]),
    });
  });

  it("has sort key sk of type STRING", () => {
    template.hasResourceProperties("AWS::DynamoDB::GlobalTable", {
      AttributeDefinitions: Match.arrayWith([
        { AttributeName: "sk", AttributeType: "S" },
      ]),
      KeySchema: Match.arrayWith([{ AttributeName: "sk", KeyType: "RANGE" }]),
    });
  });

  it("has GSI entity-type-index with correct keys", () => {
    template.hasResourceProperties("AWS::DynamoDB::GlobalTable", {
      GlobalSecondaryIndexes: Match.arrayWith([
        Match.objectLike({
          IndexName: "entity-type-index",
          KeySchema: Match.arrayWith([
            { AttributeName: "entityType", KeyType: "HASH" },
            { AttributeName: "pk", KeyType: "RANGE" },
          ]),
        }),
      ]),
      AttributeDefinitions: Match.arrayWith([
        { AttributeName: "entityType", AttributeType: "S" },
      ]),
    });
  });

  it("uses on-demand (PAY_PER_REQUEST) billing", () => {
    template.hasResourceProperties("AWS::DynamoDB::GlobalTable", {
      BillingMode: "PAY_PER_REQUEST",
    });
  });

  it("has DeletionPolicy Retain", () => {
    template.hasResource("AWS::DynamoDB::GlobalTable", {
      DeletionPolicy: "Retain",
      UpdateReplacePolicy: "Retain",
    });
  });
});

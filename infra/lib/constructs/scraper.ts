import * as cdk from "aws-cdk-lib";
import {
  Code,
  Function as LambdaFunction,
  Runtime,
} from "aws-cdk-lib/aws-lambda";
import { Role, ServicePrincipal } from "aws-cdk-lib/aws-iam";
import { CfnSchedule } from "aws-cdk-lib/aws-scheduler";
import { Construct } from "constructs";
import type { TableV2 } from "aws-cdk-lib/aws-dynamodb";

interface ScraperProps {
  table: TableV2;
}

const SCRAPER_TIMEOUT_MINUTES = 5;

export class FringeScraper extends Construct {
  public constructor(
    scope: Readonly<Construct>,
    id: string,
    props: Readonly<ScraperProps>,
  ) {
    super(scope, id);

    const fn = new LambdaFunction(this, "ScraperFunction", {
      runtime: Runtime.DOTNET_10,
      handler: "FringeScraper::FringeScraper.LambdaHandler::FunctionHandler",
      code: Code.fromAsset("../Fringe.Scraper/publish"),
      timeout: cdk.Duration.minutes(SCRAPER_TIMEOUT_MINUTES),
      memorySize: 512,
      environment: {
        DYNAMO_TABLE_NAME: props.table.tableName,
      },
    });

    props.table.grantReadWriteData(fn);

    const schedulerRole = new Role(this, "ScraperSchedulerRole", {
      assumedBy: new ServicePrincipal("scheduler.amazonaws.com"),
    });
    fn.grantInvoke(schedulerRole);

    new CfnSchedule(this, "NightlySchedule", {
      scheduleExpression: "cron(0 3 * * ? *)",
      scheduleExpressionTimezone: "America/Edmonton",
      flexibleTimeWindow: { mode: "OFF" },
      target: {
        arn: fn.functionArn,
        roleArn: schedulerRole.roleArn,
      },
      description: "Trigger Fringe scraper nightly at 3am Mountain time",
    });
  }
}

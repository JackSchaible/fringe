import * as cdk from 'aws-cdk-lib';
import { TableV2 } from 'aws-cdk-lib/aws-dynamodb';
import { Runtime, Function as LambdaFunction, Code } from 'aws-cdk-lib/aws-lambda';
import { CfnSchedule } from 'aws-cdk-lib/aws-scheduler';
import { Role, ServicePrincipal } from 'aws-cdk-lib/aws-iam';
import { Construct } from 'constructs';

interface ScraperProps {
  table: TableV2;
}

export class FringeScraper extends Construct {
  constructor(scope: Construct, id: string, props: ScraperProps) {
    super(scope, id);

    const fn = new LambdaFunction(this, 'ScraperFunction', {
      runtime: Runtime.DOTNET_10,
      handler: 'FringeScraper::FringeScraper.LambdaHandler::FunctionHandler',
      code: Code.fromAsset('../Fringe.Scraper/publish'),
      timeout: cdk.Duration.minutes(5),
      memorySize: 512,
      environment: {
        DYNAMO_TABLE_NAME: props.table.tableName,
      },
    });

    props.table.grantReadWriteData(fn);

    const schedulerRole = new Role(this, 'ScraperSchedulerRole', {
      assumedBy: new ServicePrincipal('scheduler.amazonaws.com'),
    });
    fn.grantInvoke(schedulerRole);

    new CfnSchedule(this, 'NightlySchedule', {
      scheduleExpression: 'cron(0 3 * * ? *)',
      scheduleExpressionTimezone: 'America/Edmonton',
      flexibleTimeWindow: { mode: 'OFF' },
      target: {
        arn: fn.functionArn,
        roleArn: schedulerRole.roleArn,
      },
      description: 'Trigger Fringe scraper nightly at 3am Mountain time',
    });
  }
}

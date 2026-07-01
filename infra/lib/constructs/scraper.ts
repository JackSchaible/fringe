import * as cdk from 'aws-cdk-lib';
import { TableV2 } from 'aws-cdk-lib/aws-dynamodb';
import { Runtime, Function as LambdaFunction, Code } from 'aws-cdk-lib/aws-lambda';
import { Rule, Schedule } from 'aws-cdk-lib/aws-events';
import { LambdaFunction as LambdaTarget } from 'aws-cdk-lib/aws-events-targets';
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

    new Rule(this, 'NightlyRule', {
      schedule: Schedule.cron({ hour: '3', minute: '0' }),
      targets: [new LambdaTarget(fn)],
      description: 'Trigger Fringe scraper nightly at 3am UTC',
    });
  }
}

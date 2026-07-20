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

interface TransferMatrixProps {
  table: TableV2;
}

const TRANSFER_MATRIX_TIMEOUT_MINUTES = 3;

/**
 * Nightly Lambda that (re)generates the venue transfer matrix. Scheduled an hour after the
 * scraper's nightly run so canonical venue coordinates are settled first, but it never invokes
 * or depends on the scraper directly — the two Lambdas only agree on a time window.
 * `reservedConcurrentExecutions: 1` guards against an overlapping run firing a second round of
 * (rate-limited, quota-metered) OpenRouteService matrix requests.
 */
export class FringeTransferMatrix extends Construct {
  public constructor(
    scope: Readonly<Construct>,
    id: string,
    props: Readonly<TransferMatrixProps>,
  ) {
    super(scope, id);

    const fn = new LambdaFunction(this, "TransferMatrixFunction", {
      runtime: Runtime.DOTNET_10,
      handler:
        "FringeTransferMatrix::FringeTransferMatrix.LambdaHandler::FunctionHandler",
      code: Code.fromAsset("../Fringe.TransferMatrix/publish"),
      timeout: cdk.Duration.minutes(TRANSFER_MATRIX_TIMEOUT_MINUTES),
      memorySize: 256,
      reservedConcurrentExecutions: 1,
      environment: {
        DYNAMO_TABLE_NAME: props.table.tableName,
        OPENROUTESERVICE_API_KEY: process.env.OPENROUTESERVICE_API_KEY ?? "",
      },
    });

    props.table.grantReadWriteData(fn);

    const schedulerRole = new Role(this, "TransferMatrixSchedulerRole", {
      assumedBy: new ServicePrincipal("scheduler.amazonaws.com"),
    });
    fn.grantInvoke(schedulerRole);

    new CfnSchedule(this, "NightlySchedule", {
      scheduleExpression: "cron(0 4 * * ? *)",
      scheduleExpressionTimezone: "America/Edmonton",
      flexibleTimeWindow: { mode: "OFF" },
      target: {
        arn: fn.functionArn,
        roleArn: schedulerRole.roleArn,
      },
      description:
        "Trigger Fringe transfer-matrix generation nightly at 4am Mountain time, after the scraper",
    });
  }
}

import * as cdk from "aws-cdk-lib";
import {
  Code,
  Function as LambdaFunction,
  Runtime,
} from "aws-cdk-lib/aws-lambda";
import {
  DomainName,
  EndpointType,
  LambdaRestApi,
  SecurityPolicy,
} from "aws-cdk-lib/aws-apigateway";
import type { Certificate } from "aws-cdk-lib/aws-certificatemanager";
import { Construct } from "constructs";
import type { FringeAuth } from "./auth";
import { PolicyStatement } from "aws-cdk-lib/aws-iam";
import type { TableV2 } from "aws-cdk-lib/aws-dynamodb";

interface ApiProps {
  table: TableV2;
  certificate: Certificate;
  auth: FringeAuth;
}

const API_TIMEOUT_SECONDS = 30;

export class FringeApi extends Construct {
  public readonly apiDomainTarget: string;

  public constructor(
    scope: Readonly<Construct>,
    id: string,
    props: Readonly<ApiProps>,
  ) {
    super(scope, id);

    const fn = new LambdaFunction(this, "ApiFunction", {
      runtime: Runtime.DOTNET_10,
      handler: "Fringe.API",
      code: Code.fromAsset("../Fringe.API/publish"),
      timeout: cdk.Duration.seconds(API_TIMEOUT_SECONDS),
      memorySize: 512,
      environment: {
        DYNAMO_TABLE_NAME: props.table.tableName,
        ALLOWED_ORIGINS:
          "https://fringe.jackschaible.ca;https://localhost:4200",
        COGNITO_USER_POOL_ID: props.auth.userPool.userPoolId,
        TURNSTILE_SECRET_KEY: process.env.TURNSTILE_SECRET_KEY ?? "",
      },
    });

    props.table.grantReadWriteData(fn);
    fn.addToRolePolicy(
      new PolicyStatement({
        actions: ["cognito-idp:AdminDeleteUser"],
        resources: [props.auth.userPool.userPoolArn],
      }),
    );

    const api = new LambdaRestApi(this, "RestApi", {
      handler: fn,
      proxy: true,
    });

    const domain = new DomainName(this, "ApiDomain", {
      domainName: "api.fringe.jackschaible.ca",
      certificate: props.certificate,
      securityPolicy: SecurityPolicy.TLS_1_2,
      endpointType: EndpointType.EDGE,
    });

    domain.addBasePathMapping(api);

    this.apiDomainTarget = domain.domainNameAliasDomainName;
  }
}

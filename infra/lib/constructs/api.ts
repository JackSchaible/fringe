import * as cdk from "aws-cdk-lib";
import {
  ARecord,
  type IHostedZone,
  RecordTarget,
} from "aws-cdk-lib/aws-route53";
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
import { ApiGatewayDomain } from "aws-cdk-lib/aws-route53-targets";
import type { Certificate } from "aws-cdk-lib/aws-certificatemanager";
import { Construct } from "constructs";
import type { FringeAuth } from "./auth";
import { PolicyStatement } from "aws-cdk-lib/aws-iam";
import type { TableV2 } from "aws-cdk-lib/aws-dynamodb";

interface ApiProps {
  table: TableV2;
  certificate: Certificate;
  auth: FringeAuth;
  hostedZone: IHostedZone;
}

const API_TIMEOUT_SECONDS = 30;
const API_SUBDOMAIN = "api";
const API_DOMAIN_NAME = "api.fringequest.app";

export class FringeApi extends Construct {
  public readonly apiDomainTarget: string;

  public constructor(
    scope: Readonly<Construct>,
    id: string,
    props: Readonly<ApiProps>,
  ) {
    super(scope, id);

    const fn = this.createLambda(props);

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
      domainName: API_DOMAIN_NAME,
      certificate: props.certificate,
      securityPolicy: SecurityPolicy.TLS_1_2,
      endpointType: EndpointType.EDGE,
    });

    domain.addBasePathMapping(api);

    new ARecord(this, "ApiAliasRecord", {
      zone: props.hostedZone,
      recordName: API_SUBDOMAIN,
      target: RecordTarget.fromAlias(new ApiGatewayDomain(domain)),
    });

    this.apiDomainTarget = domain.domainNameAliasDomainName;
  }

  private createLambda(props: Readonly<ApiProps>): LambdaFunction {
    return new LambdaFunction(this, "ApiFunction", {
      runtime: Runtime.DOTNET_10,
      handler: "Fringe.API",
      code: Code.fromAsset("../Fringe.API/publish"),
      timeout: cdk.Duration.seconds(API_TIMEOUT_SECONDS),
      memorySize: 512,
      environment: {
        DYNAMO_TABLE_NAME: props.table.tableName,
        ALLOWED_ORIGINS: "https://fringequest.app;https://localhost:4200",
        COGNITO_USER_POOL_ID: props.auth.userPool.userPoolId,
        TURNSTILE_SECRET_KEY: process.env.TURNSTILE_SECRET_KEY ?? "",
      },
    });
  }
}

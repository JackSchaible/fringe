import * as cdk from 'aws-cdk-lib';
import { TableV2 } from 'aws-cdk-lib/aws-dynamodb';
import { Runtime, Function as LambdaFunction, Code } from 'aws-cdk-lib/aws-lambda';
import { LambdaRestApi, DomainName, SecurityPolicy, EndpointType } from 'aws-cdk-lib/aws-apigateway';
import { Certificate } from 'aws-cdk-lib/aws-certificatemanager';
import { Construct } from 'constructs';
import { FringeAuth } from './auth';

interface ApiProps {
  table: TableV2;
  certificate: Certificate;
  auth: FringeAuth;
}

export class FringeApi extends Construct {
  public readonly apiDomainTarget: string;

  constructor(scope: Construct, id: string, props: ApiProps) {
    super(scope, id);

    const fn = new LambdaFunction(this, 'ApiFunction', {
      runtime: Runtime.DOTNET_10,
      handler: 'Fringe.API',
      code: Code.fromAsset('../Fringe.API/publish'),
      timeout: cdk.Duration.seconds(30),
      memorySize: 512,
      environment: {
        DYNAMO_TABLE_NAME: props.table.tableName,
        ALLOWED_ORIGINS: 'https://fringe.jackschaible.ca;https://localhost:4200',
        COGNITO_USER_POOL_ID: props.auth.userPool.userPoolId,
        TURNSTILE_SECRET_KEY: process.env['TURNSTILE_SECRET_KEY'] ?? '',
      },
    });

    props.table.grantReadWriteData(fn);

    const api = new LambdaRestApi(this, 'RestApi', {
      handler: fn,
      proxy: true,
    });

    const domain = new DomainName(this, 'ApiDomain', {
      domainName: 'api.fringe.jackschaible.ca',
      certificate: props.certificate,
      securityPolicy: SecurityPolicy.TLS_1_2,
      endpointType: EndpointType.EDGE,
    });

    domain.addBasePathMapping(api);

    this.apiDomainTarget = domain.domainNameAliasDomainName;
  }
}

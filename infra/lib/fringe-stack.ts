import * as cdk from 'aws-cdk-lib';
import { Certificate } from 'aws-cdk-lib/aws-certificatemanager';
import { Construct } from 'constructs';
import { FringeDynamo } from './constructs/dynamo';
import { FringeApi } from './constructs/api';
import { FringeAuth } from './constructs/auth';
import { FringeScraper } from './constructs/scraper';
import { FringeFrontend } from './constructs/frontend';

interface FringeStackProps extends cdk.StackProps {
  certificate: Certificate;
}

export class FringeStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props: FringeStackProps) {
    super(scope, id, props);

    const { certificate } = props;

    cdk.Tags.of(this).add('project', 'fringe-app');

    const dynamo = new FringeDynamo(this, 'Dynamo');
    const auth = new FringeAuth(this, 'Auth');
    const frontend = new FringeFrontend(this, 'Frontend', { certificate });
    const api = new FringeApi(this, 'Api', { table: dynamo.table, certificate, auth });
    new FringeScraper(this, 'Scraper', { table: dynamo.table });

    new cdk.CfnOutput(this, 'FrontendUrl', {
      value: `https://fringe.jackschaible.ca`,
      description: 'Point CNAME fringe.jackschaible.ca to this CloudFront domain',
    });
    new cdk.CfnOutput(this, 'CloudFrontDomain', {
      value: frontend.distributionDomain,
    });
    new cdk.CfnOutput(this, 'ApiUrl', {
      value: `https://api.fringe.jackschaible.ca`,
      description: 'Point CNAME api.fringe.jackschaible.ca to the API Gateway domain shown below',
    });
    new cdk.CfnOutput(this, 'ApiGatewayDomain', {
      value: api.apiDomainTarget,
    });
    new cdk.CfnOutput(this, 'CognitoUserPoolId', {
      value: auth.userPool.userPoolId,
      description: 'Set cognitoUserPoolId in src/environments/environment.prod.ts',
    });
    new cdk.CfnOutput(this, 'CognitoClientId', {
      value: auth.userPoolClient.userPoolClientId,
      description: 'Set cognitoClientId in src/environments/environment.prod.ts',
    });
  }
}

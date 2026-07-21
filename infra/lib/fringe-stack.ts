import * as cdk from "aws-cdk-lib";
import type { Certificate } from "aws-cdk-lib/aws-certificatemanager";
import type { Construct } from "constructs";
import { FringeApi } from "./constructs/api";
import { FringeAuth } from "./constructs/auth";
import { FringeDynamo } from "./constructs/dynamo";
import { FringeFrontend } from "./constructs/frontend";
import { FringeScraper } from "./constructs/scraper";
import { FringeTransferMatrix } from "./constructs/transfer-matrix";
import type { IHostedZone } from "aws-cdk-lib/aws-route53";

interface FringeStackProps extends cdk.StackProps {
  certificate: Certificate;
  hostedZone: IHostedZone;
}

interface StackConstructs {
  dynamo: FringeDynamo;
  auth: FringeAuth;
  frontend: FringeFrontend;
  api: FringeApi;
}

export class FringeStack extends cdk.Stack {
  public constructor(
    scope: Readonly<Construct>,
    id: string,
    props: Readonly<FringeStackProps>,
  ) {
    super(scope, id, { ...props, terminationProtection: true });

    const { certificate, hostedZone } = props;
    cdk.Tags.of(this).add("project", "fringe-app");

    const constructs = this.createConstructs(certificate, hostedZone);
    this.createOutputs(constructs);
  }

  private createConstructs(
    certificate: Certificate,
    hostedZone: Readonly<IHostedZone>,
  ): StackConstructs {
    const dynamo = new FringeDynamo(this, "Dynamo");
    const auth = new FringeAuth(this, "Auth");
    const frontend = new FringeFrontend(this, "Frontend", {
      certificate,
      hostedZone,
    });
    const api = new FringeApi(this, "Api", {
      table: dynamo.table,
      certificate,
      auth,
      hostedZone,
    });
    new FringeScraper(this, "Scraper", { table: dynamo.table });
    new FringeTransferMatrix(this, "TransferMatrix", { table: dynamo.table });

    return { dynamo, auth, frontend, api };
  }

  private createOutputs({
    frontend,
    api,
    auth,
  }: Readonly<StackConstructs>): void {
    new cdk.CfnOutput(this, "FrontendUrl", {
      value: `https://fringequest.app`,
    });
    new cdk.CfnOutput(this, "CloudFrontDomain", {
      value: frontend.distributionDomain,
      description:
        "Route53 alias target for fringequest.app (managed automatically by this stack)",
    });
    new cdk.CfnOutput(this, "ApiUrl", {
      value: `https://api.fringequest.app`,
    });
    new cdk.CfnOutput(this, "ApiGatewayDomain", {
      value: api.apiDomainTarget,
      description:
        "Route53 alias target for api.fringequest.app (managed automatically by this stack)",
    });
    new cdk.CfnOutput(this, "CognitoUserPoolId", {
      value: auth.userPool.userPoolId,
      description:
        "Set cognitoUserPoolId in src/environments/environment.prod.ts",
    });
    new cdk.CfnOutput(this, "CognitoClientId", {
      value: auth.userPoolClient.userPoolClientId,
      description:
        "Set cognitoClientId in src/environments/environment.prod.ts",
    });
  }
}

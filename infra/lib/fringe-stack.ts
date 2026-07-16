import * as cdk from "aws-cdk-lib";
import type { Certificate } from "aws-cdk-lib/aws-certificatemanager";
import type { Construct } from "constructs";
import { FringeApi } from "./constructs/api";
import { FringeAuth } from "./constructs/auth";
import { FringeDynamo } from "./constructs/dynamo";
import { FringeFrontend } from "./constructs/frontend";
import { FringeScraper } from "./constructs/scraper";

interface FringeStackProps extends cdk.StackProps {
  certificate: Certificate;
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

    const { certificate } = props;
    cdk.Tags.of(this).add("project", "fringe-app");

    const constructs = this.createConstructs(certificate);
    this.createOutputs(constructs);
  }

  private createConstructs(
    certificate: Readonly<Certificate>,
  ): StackConstructs {
    const dynamo = new FringeDynamo(this, "Dynamo");
    const auth = new FringeAuth(this, "Auth");
    const frontend = new FringeFrontend(this, "Frontend", { certificate });
    const api = new FringeApi(this, "Api", {
      table: dynamo.table,
      certificate,
      auth,
    });
    new FringeScraper(this, "Scraper", { table: dynamo.table });

    return { dynamo, auth, frontend, api };
  }

  private createOutputs({ frontend, api, auth }: Readonly<StackConstructs>): void {
    new cdk.CfnOutput(this, "FrontendUrl", {
      value: `https://fringe.jackschaible.ca`,
      description:
        "Point CNAME fringe.jackschaible.ca to this CloudFront domain",
    });
    new cdk.CfnOutput(this, "CloudFrontDomain", {
      value: frontend.distributionDomain,
    });
    new cdk.CfnOutput(this, "ApiUrl", {
      value: `https://api.fringe.jackschaible.ca`,
      description:
        "Point CNAME api.fringe.jackschaible.ca to the API Gateway domain shown below",
    });
    new cdk.CfnOutput(this, "ApiGatewayDomain", {
      value: api.apiDomainTarget,
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

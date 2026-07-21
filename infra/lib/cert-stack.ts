import * as cdk from "aws-cdk-lib";
import {
  Certificate,
  CertificateValidation,
} from "aws-cdk-lib/aws-certificatemanager";
import type { Construct } from "constructs";
import { HostedZone } from "aws-cdk-lib/aws-route53";

const DOMAIN_NAME = "fringequest.app";
const API_DOMAIN_NAME = "api.fringequest.app";

export class CertStack extends cdk.Stack {
  public readonly certificate: Certificate;
  public readonly hostedZone: HostedZone;

  public constructor(
    scope: Readonly<Construct>,
    id: string,
    props?: Readonly<cdk.StackProps>,
  ) {
    super(scope, id, props);

    this.hostedZone = new HostedZone(this, "HostedZone", {
      zoneName: DOMAIN_NAME,
    });

    this.certificate = new Certificate(this, "Certificate", {
      domainName: DOMAIN_NAME,
      subjectAlternativeNames: [API_DOMAIN_NAME],
      validation: CertificateValidation.fromDns(this.hostedZone),
    });

    new cdk.CfnOutput(this, "NameServers", {
      value: cdk.Fn.join(", ", this.hostedZone.hostedZoneNameServers ?? []),
      description:
        "Set these as the nameservers for fringequest.app at the registrar (one-time). Everything else DNS-wise is then managed by this stack.",
    });
  }
}

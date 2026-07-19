import * as cdk from "aws-cdk-lib";
import {
  Certificate,
  CertificateValidation,
} from "aws-cdk-lib/aws-certificatemanager";
import type { Construct } from "constructs";

export class CertStack extends cdk.Stack {
  public readonly certificate: Certificate;

  public constructor(
    scope: Readonly<Construct>,
    id: string,
    props?: Readonly<cdk.StackProps>,
  ) {
    super(scope, id, props);

    this.certificate = new Certificate(this, "Certificate", {
      domainName: "fringe.jackschaible.ca",
      subjectAlternativeNames: ["api.fringe.jackschaible.ca"],
      validation: CertificateValidation.fromDns(),
    });
  }
}

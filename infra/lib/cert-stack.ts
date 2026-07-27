import * as cdk from "aws-cdk-lib";
import {
  Certificate,
  CertificateValidation,
} from "aws-cdk-lib/aws-certificatemanager";
import { HostedZone, type IHostedZone } from "aws-cdk-lib/aws-route53";
import type { Construct } from "constructs";

const DOMAIN_NAME = "fringequest.app";
const API_DOMAIN_NAME = "api.fringequest.app";
/* Fringequest.app was registered through Route53 Domains, which auto-creates a
   hosted zone at registration time and points the domain's nameserver
   delegation at it. That zone already carries live email DNS (Fastmail
   MX/DKIM, SES DKIM), so it must be imported here rather than re-created —
   otherwise the new zone's NS values won't match the registrar delegation and
   nothing will resolve. */
const HOSTED_ZONE_ID = "Z06094931VR25WTZJNIG3";

export class CertStack extends cdk.Stack {
  public readonly certificate: Certificate;
  public readonly hostedZone: IHostedZone;

  public constructor(
    scope: Readonly<Construct>,
    id: string,
    props?: Readonly<cdk.StackProps>,
  ) {
    super(scope, id, props);

    this.hostedZone = HostedZone.fromHostedZoneAttributes(this, "HostedZone", {
      hostedZoneId: HOSTED_ZONE_ID,
      zoneName: DOMAIN_NAME,
    });

    /* Logical id "Certificate2": FringeStack reads this cert's ARN cross-region
       (crossRegionReferences) via a writer/reader pair of custom resources.
       The reader's declared properties — and therefore whether CloudFormation
       ever re-invokes it to pick up a new value — are keyed off this
       construct's logical id. If this certificate is ever replaced again (id
       change, domain/SAN change), bump this id once more, or FringeStack's
       reader will keep serving the previous cert's ARN indefinitely even
       though the writer side updates correctly. */
    this.certificate = new Certificate(this, "Certificate2", {
      domainName: DOMAIN_NAME,
      subjectAlternativeNames: [API_DOMAIN_NAME],
      validation: CertificateValidation.fromDns(this.hostedZone),
    });
  }
}

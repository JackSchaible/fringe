import * as cdk from 'aws-cdk-lib';
import { Certificate, CertificateValidation } from 'aws-cdk-lib/aws-certificatemanager';
import { Construct } from 'constructs';

export class CertStack extends cdk.Stack {
  public readonly certificate: Certificate;

  constructor(scope: Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, props);

    this.certificate = new Certificate(this, 'Certificate', {
      domainName: 'fringe.jackschaible.ca',
      subjectAlternativeNames: ['api.fringe.jackschaible.ca'],
      validation: CertificateValidation.fromDns(),
    });
  }
}

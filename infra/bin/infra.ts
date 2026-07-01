#!/usr/bin/env node
import 'source-map-support/register';
import * as cdk from 'aws-cdk-lib';
import { CertStack } from '../lib/cert-stack';
import { FringeStack } from '../lib/fringe-stack';

const app = new cdk.App();

const certStack = new CertStack(app, 'FringeCertStack', {
  env: {
    account: process.env.CDK_DEFAULT_ACCOUNT,
    region: 'us-east-1',
  },
  crossRegionReferences: true,
});

new FringeStack(app, 'FringeStack', {
  env: {
    account: process.env.CDK_DEFAULT_ACCOUNT,
    region: process.env.CDK_DEFAULT_REGION,
  },
  crossRegionReferences: true,
  certificate: certStack.certificate,
});

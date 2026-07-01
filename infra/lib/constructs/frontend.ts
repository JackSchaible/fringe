import * as cdk from 'aws-cdk-lib';
import { Bucket, BlockPublicAccess } from 'aws-cdk-lib/aws-s3';
import { BucketDeployment, Source } from 'aws-cdk-lib/aws-s3-deployment';
import {
  Distribution,
  ViewerProtocolPolicy,
  AllowedMethods,
  CachePolicy,
  OriginAccessIdentity,
} from 'aws-cdk-lib/aws-cloudfront';
import { S3BucketOrigin } from 'aws-cdk-lib/aws-cloudfront-origins';
import { Certificate } from 'aws-cdk-lib/aws-certificatemanager';
import { Construct } from 'constructs';

interface FrontendProps {
  certificate: Certificate;
}

export class FringeFrontend extends Construct {
  public readonly distributionDomain: string;

  constructor(scope: Construct, id: string, props: FrontendProps) {
    super(scope, id);

    const bucket = new Bucket(this, 'SiteBucket', {
      blockPublicAccess: BlockPublicAccess.BLOCK_ALL,
      removalPolicy: cdk.RemovalPolicy.RETAIN,
      autoDeleteObjects: false,
    });

    const oai = new OriginAccessIdentity(this, 'OAI');
    bucket.grantRead(oai);

    const distribution = new Distribution(this, 'Distribution', {
      defaultBehavior: {
        origin: new S3BucketOrigin(bucket, { originAccessIdentity: oai }),
        viewerProtocolPolicy: ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
        allowedMethods: AllowedMethods.ALLOW_GET_HEAD,
        cachePolicy: CachePolicy.CACHING_OPTIMIZED,
      },
      domainNames: ['fringe.jackschaible.ca'],
      certificate: props.certificate,
      defaultRootObject: 'index.html',
      errorResponses: [
        { httpStatus: 403, responsePagePath: '/index.html', responseHttpStatus: 200, ttl: cdk.Duration.seconds(0) },
        { httpStatus: 404, responsePagePath: '/index.html', responseHttpStatus: 200, ttl: cdk.Duration.seconds(0) },
      ],
    });

    new BucketDeployment(this, 'Deploy', {
      sources: [Source.asset('../fringe-client/dist/client-new/browser')],
      destinationBucket: bucket,
      distribution,
      distributionPaths: ['/*'],
    });

    this.distributionDomain = distribution.distributionDomainName;
  }
}

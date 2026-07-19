import * as cdk from "aws-cdk-lib";
import {
  AllowedMethods,
  CachePolicy,
  Distribution,
  ViewerProtocolPolicy,
} from "aws-cdk-lib/aws-cloudfront";
import { BlockPublicAccess, Bucket } from "aws-cdk-lib/aws-s3";
import { BucketDeployment, Source } from "aws-cdk-lib/aws-s3-deployment";
import type { Certificate } from "aws-cdk-lib/aws-certificatemanager";
import { Construct } from "constructs";
import { S3BucketOrigin } from "aws-cdk-lib/aws-cloudfront-origins";

interface FrontendProps {
  certificate: Certificate;
}

const ERROR_RESPONSE_TTL_SECONDS = 0;

export class FringeFrontend extends Construct {
  public readonly distributionDomain: string;

  public constructor(
    scope: Readonly<Construct>,
    id: string,
    props: Readonly<FrontendProps>,
  ) {
    super(scope, id);

    const bucket = new Bucket(this, "SiteBucket", {
      blockPublicAccess: BlockPublicAccess.BLOCK_ALL,
      removalPolicy: cdk.RemovalPolicy.RETAIN,
      autoDeleteObjects: false,
    });

    const distribution = new Distribution(this, "Distribution", {
      defaultBehavior: {
        origin: S3BucketOrigin.withOriginAccessControl(bucket),
        viewerProtocolPolicy: ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
        allowedMethods: AllowedMethods.ALLOW_GET_HEAD,
        cachePolicy: CachePolicy.CACHING_OPTIMIZED,
      },
      domainNames: ["fringe.jackschaible.ca"],
      certificate: props.certificate,
      defaultRootObject: "index.html",
      errorResponses: [
        {
          httpStatus: 403,
          responsePagePath: "/index.html",
          responseHttpStatus: 200,
          ttl: cdk.Duration.seconds(ERROR_RESPONSE_TTL_SECONDS),
        },
        {
          httpStatus: 404,
          responsePagePath: "/index.html",
          responseHttpStatus: 200,
          ttl: cdk.Duration.seconds(ERROR_RESPONSE_TTL_SECONDS),
        },
      ],
    });

    new BucketDeployment(this, "Deploy", {
      sources: [Source.asset("../fringe-client/dist/client-new/browser")],
      destinationBucket: bucket,
      distribution,
      distributionPaths: ["/*"],
    });

    this.distributionDomain = distribution.distributionDomainName;
  }
}

import * as cdk from "aws-cdk-lib";
import {
  ARecord,
  AaaaRecord,
  type IHostedZone,
  RecordTarget,
} from "aws-cdk-lib/aws-route53";
import {
  AllowedMethods,
  CachePolicy,
  Function as CloudFrontFunction,
  Distribution,
  FunctionCode,
  FunctionEventType,
  FunctionRuntime,
  ViewerProtocolPolicy,
} from "aws-cdk-lib/aws-cloudfront";
import { BlockPublicAccess, Bucket } from "aws-cdk-lib/aws-s3";
import { BucketDeployment, Source } from "aws-cdk-lib/aws-s3-deployment";
import type { Certificate } from "aws-cdk-lib/aws-certificatemanager";
import { CloudFrontTarget } from "aws-cdk-lib/aws-route53-targets";
import { Construct } from "constructs";
import { S3BucketOrigin } from "aws-cdk-lib/aws-cloudfront-origins";

const DOMAIN_NAME = "fringequest.app";

interface FrontendProps {
  certificate: Certificate;
  hostedZone: IHostedZone;
}

const ERROR_RESPONSE_TTL_SECONDS = 0;

/*
 * Angular's localized build emits one full app copy per locale
 * (dist/.../browser/<locale>/). The default locale (en-CA) deploys to the
 * bucket root so the existing unprefixed URLs keep working — its <base href>
 * is explicitly overridden to "/" in angular.json so it doesn't default to
 * "/en-CA/" like the other locales. fr-CA deploys under /fr/ (also an
 * explicit baseHref override, so the URL stays short instead of /fr-CA/).
 * This function runs at the edge and rewrites app-route requests (no file
 * extension) to the right locale's index.html so deep links and
 * client-side routing resolve to the correct translation instead of
 * always falling back to the English shell.
 */
const LOCALE_ROUTER_FUNCTION_CODE = `
function handler(event) {
  var request = event.request;
  var uri = request.uri;
  var lastSegment = uri.substring(uri.lastIndexOf('/') + 1);

  // Static assets (js/css/images/etc.) are requested with their real path
  // already baked in via each locale's <base href>; leave them alone.
  if (lastSegment.indexOf('.') !== -1) {
    return request;
  }

  if (uri === '/fr' || uri.indexOf('/fr/') === 0) {
    request.uri = '/fr/index.html';
  } else {
    request.uri = '/index.html';
  }

  return request;
}
`;

export class FringeFrontend extends Construct {
  public readonly distributionDomain: string;

  public constructor(
    scope: Readonly<Construct>,
    id: string,
    props: Readonly<FrontendProps>,
  ) {
    super(scope, id);

    const bucket = this.createBucket();
    const localeRouterFunction = this.createLocaleRouterFunction();
    const distribution = this.createDistribution(
      bucket,
      localeRouterFunction,
      props.certificate,
    );
    this.deployLocales(bucket, distribution);
    this.createAliasRecords(props.hostedZone, distribution);

    this.distributionDomain = distribution.distributionDomainName;
  }

  private createBucket(): Bucket {
    return new Bucket(this, "SiteBucket", {
      blockPublicAccess: BlockPublicAccess.BLOCK_ALL,
      removalPolicy: cdk.RemovalPolicy.RETAIN,
      autoDeleteObjects: false,
    });
  }

  private createLocaleRouterFunction(): CloudFrontFunction {
    return new CloudFrontFunction(this, "LocaleRouter", {
      runtime: FunctionRuntime.JS_2_0,
      code: FunctionCode.fromInline(LOCALE_ROUTER_FUNCTION_CODE),
    });
  }

  private createDistribution(
    bucket: Readonly<Bucket>,
    localeRouterFunction: Readonly<CloudFrontFunction>,
    certificate: Readonly<Certificate>,
  ): Distribution {
    return new Distribution(this, "Distribution", {
      defaultBehavior: {
        origin: S3BucketOrigin.withOriginAccessControl(bucket),
        viewerProtocolPolicy: ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
        allowedMethods: AllowedMethods.ALLOW_GET_HEAD,
        cachePolicy: CachePolicy.CACHING_OPTIMIZED,
        functionAssociations: [
          {
            function: localeRouterFunction,
            eventType: FunctionEventType.VIEWER_REQUEST,
          },
        ],
      },
      domainNames: [DOMAIN_NAME],
      certificate,
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
  }

  private deployLocales(
    bucket: Readonly<Bucket>,
    distribution: Readonly<Distribution>,
  ): void {
    /*
     * The en-CA locale is the source locale and deploys unprefixed to the
     * bucket root, preserving today's URLs. Pruning is disabled here so
     * this deployment doesn't delete the other locales' objects out from
     * under them — each locale's own deployment prunes its own prefix.
     */
    new BucketDeployment(this, "DeployEnCA", {
      sources: [Source.asset("../fringe-client/dist/client-new/browser/en-CA")],
      destinationBucket: bucket,
      distribution,
      distributionPaths: ["/*"],
      prune: false,
    });

    new BucketDeployment(this, "DeployFrCA", {
      sources: [Source.asset("../fringe-client/dist/client-new/browser/fr-CA")],
      destinationBucket: bucket,
      destinationKeyPrefix: "fr",
      distribution,
      distributionPaths: ["/fr/*"],
    });
  }

  private createAliasRecords(
    hostedZone: Readonly<IHostedZone>,
    distribution: Readonly<Distribution>,
  ): void {
    const target = RecordTarget.fromAlias(new CloudFrontTarget(distribution));
    new ARecord(this, "AliasRecord", { zone: hostedZone, target });
    new AaaaRecord(this, "AliasRecordIpv6", { zone: hostedZone, target });
  }
}

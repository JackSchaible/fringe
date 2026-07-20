import { App, Stack } from "aws-cdk-lib";
import {
  Certificate,
  CertificateValidation,
} from "aws-cdk-lib/aws-certificatemanager";
import { Match, Template } from "aws-cdk-lib/assertions";
import { FringeFrontend } from "../../lib/constructs/frontend";
import { HostedZone } from "aws-cdk-lib/aws-route53";

describe("FringeFrontend", () => {
  let template: Template;

  beforeEach(() => {
    const app = new App();
    const stack = new Stack(app, "TestStack", {
      env: { account: "123456789012", region: "us-east-1" },
    });
    const hostedZone = new HostedZone(stack, "Zone", {
      zoneName: "fringequest.app",
    });
    const cert = new Certificate(stack, "Cert", {
      domainName: "fringequest.app",
      validation: CertificateValidation.fromDns(hostedZone),
    });
    new FringeFrontend(stack, "Frontend", { certificate: cert, hostedZone });
    template = Template.fromStack(stack);
  });

  describe("S3 Bucket", () => {
    it("creates an S3 Bucket", () => {
      template.resourceCountIs("AWS::S3::Bucket", 1);
    });

    it("blocks all public access", () => {
      template.hasResourceProperties("AWS::S3::Bucket", {
        PublicAccessBlockConfiguration: {
          BlockPublicAcls: true,
          BlockPublicPolicy: true,
          IgnorePublicAcls: true,
          RestrictPublicBuckets: true,
        },
      });
    });

    it("has DeletionPolicy Retain", () => {
      template.hasResource("AWS::S3::Bucket", {
        DeletionPolicy: "Retain",
        UpdateReplacePolicy: "Retain",
      });
    });
  });

  describe("CloudFront Distribution", () => {
    it("creates a CloudFront Distribution", () => {
      template.resourceCountIs("AWS::CloudFront::Distribution", 1);
    });

    it("has domainNames fringequest.app", () => {
      template.hasResourceProperties("AWS::CloudFront::Distribution", {
        DistributionConfig: Match.objectLike({
          Aliases: ["fringequest.app"],
        }),
      });
    });

    it("sets defaultRootObject to index.html", () => {
      template.hasResourceProperties("AWS::CloudFront::Distribution", {
        DistributionConfig: Match.objectLike({
          DefaultRootObject: "index.html",
        }),
      });
    });

    it("redirects HTTP to HTTPS", () => {
      template.hasResourceProperties("AWS::CloudFront::Distribution", {
        DistributionConfig: Match.objectLike({
          DefaultCacheBehavior: Match.objectLike({
            ViewerProtocolPolicy: "redirect-to-https",
          }),
        }),
      });
    });

    it("uses OAC (origin access control) for S3", () => {
      template.resourceCountIs("AWS::CloudFront::OriginAccessControl", 1);
    });

    it("has 403 error response mapping to /index.html with HTTP 200", () => {
      template.hasResourceProperties("AWS::CloudFront::Distribution", {
        DistributionConfig: Match.objectLike({
          CustomErrorResponses: Match.arrayWith([
            Match.objectLike({
              ErrorCode: 403,
              ResponsePagePath: "/index.html",
              ResponseCode: 200,
              ErrorCachingMinTTL: 0,
            }),
          ]),
        }),
      });
    });

    it("has 404 error response mapping to /index.html with HTTP 200", () => {
      template.hasResourceProperties("AWS::CloudFront::Distribution", {
        DistributionConfig: Match.objectLike({
          CustomErrorResponses: Match.arrayWith([
            Match.objectLike({
              ErrorCode: 404,
              ResponsePagePath: "/index.html",
              ResponseCode: 200,
              ErrorCachingMinTTL: 0,
            }),
          ]),
        }),
      });
    });

    it("associates a viewer-request Function for locale routing", () => {
      template.resourceCountIs("AWS::CloudFront::Function", 1);
      template.hasResourceProperties("AWS::CloudFront::Distribution", {
        DistributionConfig: Match.objectLike({
          DefaultCacheBehavior: Match.objectLike({
            FunctionAssociations: Match.arrayWith([
              Match.objectLike({ EventType: "viewer-request" }),
            ]),
          }),
        }),
      });
    });
  });

  describe("BucketDeployment", () => {
    it("creates a BucketDeployment custom resource per locale", () => {
      // BucketDeployment synthesizes as a Custom::CDKBucketDeployment resource
      const resources = template.findResources("Custom::CDKBucketDeployment");
      expect(Object.keys(resources).length).toBeGreaterThanOrEqual(2);
    });

    it("deploys the fr locale under a /fr destination prefix", () => {
      template.hasResourceProperties("Custom::CDKBucketDeployment", {
        DestinationBucketKeyPrefix: "fr",
      });
    });
  });

  describe("Route53 alias records", () => {
    it("creates an A record aliasing to the CloudFront distribution", () => {
      template.hasResourceProperties("AWS::Route53::RecordSet", {
        Type: "A",
        AliasTarget: Match.objectLike({
          DNSName: Match.anyValue(),
        }),
      });
    });

    it("creates an AAAA record aliasing to the CloudFront distribution", () => {
      template.hasResourceProperties("AWS::Route53::RecordSet", {
        Type: "AAAA",
        AliasTarget: Match.objectLike({
          DNSName: Match.anyValue(),
        }),
      });
    });
  });
});

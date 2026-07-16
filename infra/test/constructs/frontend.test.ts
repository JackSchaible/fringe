import { App, Stack } from "aws-cdk-lib";
import { Template, Match } from "aws-cdk-lib/assertions";
import {
  Certificate,
  CertificateValidation,
} from "aws-cdk-lib/aws-certificatemanager";
import { FringeFrontend } from "../../lib/constructs/frontend";

describe("FringeFrontend", () => {
  let template: Template;

  beforeEach(() => {
    const app = new App();
    const stack = new Stack(app, "TestStack", {
      env: { account: "123456789012", region: "us-east-1" },
    });
    const cert = new Certificate(stack, "Cert", {
      domainName: "fringe.jackschaible.ca",
      validation: CertificateValidation.fromDns(),
    });
    new FringeFrontend(stack, "Frontend", { certificate: cert });
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

    it("has domainNames fringe.jackschaible.ca", () => {
      template.hasResourceProperties("AWS::CloudFront::Distribution", {
        DistributionConfig: Match.objectLike({
          Aliases: ["fringe.jackschaible.ca"],
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
  });

  describe("BucketDeployment", () => {
    it("creates a BucketDeployment custom resource", () => {
      // BucketDeployment synthesizes as a Custom::CDKBucketDeployment resource
      const resources = template.findResources("Custom::CDKBucketDeployment");
      expect(Object.keys(resources).length).toBeGreaterThanOrEqual(1);
    });
  });
});

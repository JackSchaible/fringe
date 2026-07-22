import { Match, Template } from "aws-cdk-lib/assertions";
import { App } from "aws-cdk-lib";
import { CertStack } from "../lib/cert-stack";

describe("CertStack", () => {
  let template: Template;

  beforeEach(() => {
    const app = new App();
    const stack = new CertStack(app, "TestCertStack", {
      env: { account: "123456789012", region: "us-east-1" },
    });
    template = Template.fromStack(stack);
  });

  it("creates exactly one ACM Certificate", () => {
    template.resourceCountIs("AWS::CertificateManager::Certificate", 1);
  });

  it("issues certificate for primary domain fringequest.app", () => {
    template.hasResourceProperties("AWS::CertificateManager::Certificate", {
      DomainName: "fringequest.app",
    });
  });

  it("includes api.fringequest.app as a subject alternative name", () => {
    template.hasResourceProperties("AWS::CertificateManager::Certificate", {
      SubjectAlternativeNames: Match.arrayWith(["api.fringequest.app"]),
    });
  });

  it("uses DNS validation", () => {
    template.hasResourceProperties("AWS::CertificateManager::Certificate", {
      ValidationMethod: "DNS",
    });
  });

  it("does not create a new Route53 HostedZone (imports the existing registrar-created one)", () => {
    template.resourceCountIs("AWS::Route53::HostedZone", 0);
  });

  it("validates via DNS records in the existing hosted zone", () => {
    template.hasResourceProperties("AWS::CertificateManager::Certificate", {
      DomainValidationOptions: Match.arrayWith([
        Match.objectLike({ HostedZoneId: "Z06094931VR25WTZJNIG3" }),
      ]),
    });
  });
});

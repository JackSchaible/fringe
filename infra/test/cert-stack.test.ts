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

  it("creates exactly one Route53 HostedZone for fringequest.app", () => {
    template.resourceCountIs("AWS::Route53::HostedZone", 1);
    template.hasResourceProperties("AWS::Route53::HostedZone", {
      Name: "fringequest.app.",
    });
  });

  it("outputs the hosted zone name servers", () => {
    template.hasOutput("NameServers", {});
  });
});

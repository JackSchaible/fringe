import { App } from "aws-cdk-lib";
import { Template, Match } from "aws-cdk-lib/assertions";
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

  it("issues certificate for primary domain fringe.jackschaible.ca", () => {
    template.hasResourceProperties("AWS::CertificateManager::Certificate", {
      DomainName: "fringe.jackschaible.ca",
    });
  });

  it("includes api.fringe.jackschaible.ca as a subject alternative name", () => {
    template.hasResourceProperties("AWS::CertificateManager::Certificate", {
      SubjectAlternativeNames: Match.arrayWith(["api.fringe.jackschaible.ca"]),
    });
  });

  it("uses DNS validation", () => {
    template.hasResourceProperties("AWS::CertificateManager::Certificate", {
      ValidationMethod: "DNS",
    });
  });
});

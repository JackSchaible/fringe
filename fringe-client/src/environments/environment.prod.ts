export const environment = {
  production: true,
  apiUrl: 'https://api.fringe.jackschaible.ca',
  cognitoUserPoolId: '',   // populated from CDK output at build time
  cognitoClientId: '',     // populated from CDK output at build time
  turnstileSiteKey: '',    // populated from GitHub secret at build time
};

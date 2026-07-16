/// <reference types="@angular/localize" />

import { Amplify } from 'aws-amplify';
import { AppComponent } from './app/app';
import { appConfig } from './app/app.config';
import { bootstrapApplication } from '@angular/platform-browser';
import { environment } from './environments/environment';

if (environment.cognitoUserPoolId) {
  Amplify.configure({
    Auth: {
      Cognito: {
        userPoolClientId: environment.cognitoClientId,
        userPoolId: environment.cognitoUserPoolId,
      },
    },
  });
}

bootstrapApplication(AppComponent, appConfig).catch((err: unknown) => {
  throw err;
});

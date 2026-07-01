import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app';
import { Amplify } from 'aws-amplify';
import { environment } from './environments/environment';

if (environment.cognitoUserPoolId) {
  Amplify.configure({
    Auth: {
      Cognito: {
        userPoolId: environment.cognitoUserPoolId,
        userPoolClientId: environment.cognitoClientId,
      },
    },
  });
}

bootstrapApplication(AppComponent, appConfig).catch(err => console.error(err));

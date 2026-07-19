import { Component, input, model, output } from '@angular/core';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { faEnvelope } from '@fortawesome/pro-regular-svg-icons';
import { faPaperPlane } from '@fortawesome/pro-solid-svg-icons';

@Component({
  imports: [FormsModule, FaIconComponent, RouterLink],
  selector: 'fg-login-email-step',
  styleUrl: './email-step.scss',
  templateUrl: './email-step.html',
})
export class LoginEmailStepComponent {
  public readonly email = model.required<string>();
  public readonly agreedToTerms = model.required<boolean>();
  public readonly captchaToken = input.required<string | null>();
  public readonly loading = input.required<boolean>();
  public readonly send = output();

  protected readonly faEnvelope = faEnvelope;
  protected readonly faPaperPlane = faPaperPlane;
}

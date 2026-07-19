import { Component, input, model, output } from '@angular/core';
import {
  faArrowLeft,
  faArrowRightToBracket,
} from '@fortawesome/pro-solid-svg-icons';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { FormsModule } from '@angular/forms';
import { faKey } from '@fortawesome/pro-regular-svg-icons';

@Component({
  imports: [FormsModule, FaIconComponent],
  selector: 'fg-login-otp-step',
  styleUrl: './otp-step.scss',
  templateUrl: './otp-step.html',
})
export class LoginOtpStepComponent {
  public readonly otp = model.required<string>();
  public readonly email = input.required<string>();
  public readonly loading = input.required<boolean>();
  public readonly verify = output();
  public readonly back = output();

  protected readonly faKey = faKey;
  protected readonly faArrowRightToBracket = faArrowRightToBracket;
  protected readonly faArrowLeft = faArrowLeft;
}

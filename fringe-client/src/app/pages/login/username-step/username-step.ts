import { Component, input, model, output } from '@angular/core';
import {
  faArrowRightToBracket,
  faUser,
} from '@fortawesome/pro-solid-svg-icons';
import { FaIconComponent } from '@fortawesome/angular-fontawesome';
import { FormsModule } from '@angular/forms';

@Component({
  imports: [FormsModule, FaIconComponent],
  selector: 'fg-login-username-step',
  styleUrl: './username-step.scss',
  templateUrl: './username-step.html',
})
export class LoginUsernameStepComponent {
  public readonly username = model.required<string>();
  public readonly loading = input.required<boolean>();
  public readonly save = output();

  protected readonly faUser = faUser;
  protected readonly faArrowRightToBracket = faArrowRightToBracket;
}

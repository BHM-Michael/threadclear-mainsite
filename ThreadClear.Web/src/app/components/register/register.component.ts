import { Component, OnInit } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { RegistrationService, RegistrationRequest } from '../../services/registration.service';
import { TaxonomyService, IndustryType } from '../../services/taxonomy.service';
import { AuthService } from 'src/app/services/auth.service';

@Component({
  selector: 'app-register',
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.scss']
})
export class RegisterComponent implements OnInit {
  email = '';
  password = '';
  confirmPassword = '';
  firstName = '';
  lastName = '';

  industries: IndustryType[] = [];
  inviteToken: string | null = null;
  inviteEmail: string | null = null;

  isLoading = false;
  isValidatingToken = true;
  tokenValid = false;
  error: string | null = null;

  constructor(
    private registrationService: RegistrationService,
    private taxonomyService: TaxonomyService,
    private authService: AuthService,
    private router: Router,
    private route: ActivatedRoute
  ) { }

  ngOnInit(): void {
    // Check for invite token
    this.route.queryParams.subscribe(params => {
      this.inviteToken = params['invite'] || params['token'] || null;

      if (!this.inviteToken) {
        // No invite token - show "invitation only" message
        this.isValidatingToken = false;
        this.tokenValid = true;
      } else {
        // Validate the invite token
        this.validateInviteToken();
      }
    });
  }

  validateInviteToken(): void {
    this.isValidatingToken = true;

    this.registrationService.validateInvite(this.inviteToken!).subscribe({
      next: (response) => {
        if (response.success && response.valid) {
          this.tokenValid = true;
          this.inviteEmail = response.email || null;
          if (this.inviteEmail) {
            this.email = this.inviteEmail;
          }
        } else {
          this.tokenValid = false;
          this.error = response.message || 'Invalid or expired invite';
        }
        this.isValidatingToken = false;
      },
      error: (err) => {
        this.tokenValid = false;
        this.error = 'Unable to validate invite. Please contact your administrator.';
        this.isValidatingToken = false;
      }
    });
  }

  get isValid(): boolean {
    return !!(
      this.email &&
      this.password &&
      this.password === this.confirmPassword &&
      this.password.length >= 8
    );
  }
  get isEmailLocked(): boolean {
    return !!this.inviteEmail;
  }

  register(): void {
    if (!this.isValid) return;

    this.isLoading = true;
    this.error = null;

    const request: RegistrationRequest = {
      email: this.email,
      password: this.password,
      firstName: this.firstName || undefined,
      lastName: this.lastName || undefined,
      inviteToken: this.inviteToken || undefined
    };

    this.registrationService.register(request).subscribe({
      next: (response) => {
        console.log('Registration response:', response);
        const success = (response as any).success || (response as any).Success;
        if (success) {
          const user = (response as any).user || (response as any).User;

          if (user) {
            // Normalize property casing (same as login)
            if (user.Permissions) {
              user.permissions = user.Permissions;
            }
            if (user.DisplayName) {
              user.displayName = user.DisplayName;
            }
            if (user.Role) {
              user.role = user.Role;
            }

            localStorage.setItem('currentUser', JSON.stringify(user));
            localStorage.setItem('userCredentials', btoa(`${this.email}:${this.password}`));

            this.authService.updateCurrentUser(user);
          }

          this.router.navigate(['/analyze']);
        } else {
          this.error = response.error || 'Registration failed';
        }
        this.isLoading = false;
      },
      error: (err) => {
        this.error = err.error?.error || 'Registration failed. Please try again.';
        this.isLoading = false;
      }
    });
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }

  contactSales(): void {
    window.location.href = 'https://threadclear.com/contact';
  }
}

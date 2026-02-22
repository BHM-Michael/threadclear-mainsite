import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent {
  email = '';
  password = '';
  error = '';
  loading = false;

  constructor(
    private authService: AuthService,
    private router: Router,
    private apiService: ApiService
  ) {
    // Redirect if already logged in
    if (this.authService.isLoggedIn) {
      this.handleRedirect();
    }
  }

  ngOnInit(): void {
    // Silently wake the database before the user attempts login
    this.apiService.warmup().subscribe();
  }

  login() {
    if (!this.email || !this.password) {
      this.error = 'Please enter email and password';
      return;
    }

    this.loading = true;
    this.error = '';

    this.authService.login(this.email, this.password).subscribe({
      next: (response: any) => {
        this.loading = false;
        const success = response.success || response.Success;



        if (success) {

          this.handleRedirect();
        } else {
          const error = response.error || response.Error;
          this.error = error || 'Login failed';
        }
      },
      // In your login submit handler, in the error callback:
      error: (err) => {
        this.loading = false;
        const errorMsg = err.error?.error || err.error?.Error || 'Login failed';

        if (errorMsg.includes('pending approval')) {
          this.error = 'Your account is pending approval. You will receive an email once approved.';
        } else {
          this.error = errorMsg;
        }
      }
    });
  }

  private handleRedirect() {
    // Check for returnUrl (e.g., from connect page)
    const returnUrl = localStorage.getItem('returnUrl');

    if (returnUrl) {
      localStorage.removeItem('returnUrl');
      this.router.navigateByUrl(returnUrl);
    } else {
      this.router.navigate(['/analyze']);
    }
  }
}
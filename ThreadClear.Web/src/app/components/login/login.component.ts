import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

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
    private router: Router
  ) {
    // Redirect if already logged in
    if (this.authService.isLoggedIn) {
      this.handleRedirect();
    }
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

        console.log("Success value:", success);

        if (success) {
          console.log("About to navigate...");
          this.handleRedirect();
        } else {
          const error = response.error || response.Error;
          this.error = error || 'Login failed';
        }
      },
      error: (err) => {
        this.loading = false;
        this.error = 'Login failed. Please check your credentials.';
        console.error(err);
      }
    });
  }

  private handleRedirect() {
    // Check for returnUrl (e.g., from connect page)
    const returnUrl = localStorage.getItem('returnUrl');

    if (returnUrl) {
      localStorage.removeItem('returnUrl');
      console.log("Redirecting to returnUrl:", returnUrl);
      this.router.navigateByUrl(returnUrl).then(
        (navigated) => console.log("Navigation result:", navigated),
        (error) => console.log("Navigation error:", error)
      );
    } else {
      this.router.navigate(['/analyze']).then(
        (navigated) => console.log("Navigation result:", navigated),
        (error) => console.log("Navigation error:", error)
      );
    }
  }
}
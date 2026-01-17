import { Component, OnInit } from '@angular/core';
import { AuthService } from '../../services/auth.service';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-profile',
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.scss']
})
export class ProfileComponent implements OnInit {
  user: any = null;
  usage: any = null;
  loading = true;
  upgrading = false;
  error = '';

  gmailConnected = false;
  gmailEmail = '';
  gmailConnecting = false;
  gmailStatus = ''; // 'connected' | 'error' | ''

  // Edit mode
  editingName = false;
  editDisplayName = '';
  savingName = false;

  // Password change
  showPasswordForm = false;
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  savingPassword = false;
  passwordError = '';
  passwordSuccess = '';

  // Stripe price IDs
  readonly PRO_PRICE_ID = 'price_1SqK1qFCegaUzUfPwrO5qEDF';
  readonly ENTERPRISE_PRICE_ID = 'price_1SqK2JFCegaUzUfPXooTuvHF';

  constructor(
    private authService: AuthService,
    private http: HttpClient
  ) { }

  ngOnInit(): void {
    // Check for gmail callback status
    const urlParams = new URLSearchParams(window.location.search);
    this.gmailStatus = urlParams.get('gmail') || '';

    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.user = user;
        this.loadUsage();
        this.loadGmailStatus();
      }
    });
  }

  loadUsage(): void {
    const headers = this.getAuthHeaders();
    this.http.get<any>(`${environment.apiUrl}/usage/me`, { headers })
      .subscribe({
        next: (response) => {
          this.usage = response;
          this.loading = false;
        },
        error: (err) => {
          console.error('Failed to load usage', err);
          this.loading = false;
        }
      });
  }

  loadGmailStatus(): void {
    const headers = this.getAuthHeaders();
    const userId = this.user?.id || this.user?.Id;
    this.http.get<any>(`${environment.apiUrl}/integrations/${userId}/gmail`, { headers })
      .subscribe({
        next: (response) => {
          this.gmailConnected = response.connected;
          this.gmailEmail = response.email || '';
        },
        error: () => {
          this.gmailConnected = false;
        }
      });
  }

  connectGmail(): void {
    const userId = this.user?.id || this.user?.Id;
    window.location.href = `${environment.apiUrl}/gmail/connect?userId=${userId}`;
  }

  // Name editing
  startEditName(): void {
    this.editDisplayName = this.user?.displayName || '';
    this.editingName = true;
  }

  cancelEditName(): void {
    this.editingName = false;
    this.editDisplayName = '';
  }

  saveName(): void {
    if (!this.editDisplayName.trim()) return;

    this.savingName = true;
    const headers = this.getAuthHeaders();

    this.http.put<any>(`${environment.apiUrl}/users/me/profile`,
      { displayName: this.editDisplayName.trim() },
      { headers }
    ).subscribe({
      next: (response) => {
        if (response.success) {
          this.user.displayName = response.displayName;
          this.editingName = false;
          // Update auth service so navbar reflects change
          this.authService.updateCurrentUser({ ...this.user, displayName: response.displayName });
        }
        this.savingName = false;
      },
      error: (err) => {
        console.error('Failed to update name', err);
        this.savingName = false;
      }
    });
  }

  // Password change
  togglePasswordForm(): void {
    this.showPasswordForm = !this.showPasswordForm;
    this.resetPasswordForm();
  }

  resetPasswordForm(): void {
    this.currentPassword = '';
    this.newPassword = '';
    this.confirmPassword = '';
    this.passwordError = '';
    this.passwordSuccess = '';
  }

  changePassword(): void {
    this.passwordError = '';
    this.passwordSuccess = '';

    if (!this.currentPassword || !this.newPassword || !this.confirmPassword) {
      this.passwordError = 'All fields are required';
      return;
    }

    if (this.newPassword !== this.confirmPassword) {
      this.passwordError = 'New passwords do not match';
      return;
    }

    if (this.newPassword.length < 8) {
      this.passwordError = 'Password must be at least 8 characters';
      return;
    }

    this.savingPassword = true;
    const headers = this.getAuthHeaders();

    this.http.put<any>(`${environment.apiUrl}/users/me/password`,
      { currentPassword: this.currentPassword, newPassword: this.newPassword },
      { headers }
    ).subscribe({
      next: (response) => {
        if (response.success) {
          this.passwordSuccess = 'Password changed successfully';
          this.resetPasswordForm();
          // Update stored credentials
          const newCredentials = btoa(`${this.user.email}:${this.newPassword}`);
          localStorage.setItem('userCredentials', newCredentials);
        }
        this.savingPassword = false;
      },
      error: (err) => {
        console.error('Failed to change password', err);
        this.passwordError = err.error?.error || 'Failed to change password';
        this.savingPassword = false;
      }
    });
  }

  get currentPlan(): string {
    return this.user?.plan || 'free';
  }

  get analysesUsed(): number {
    return this.usage?.analysesUsed || 0;
  }

  get analysesLimit(): number {
    return this.usage?.analysesLimit || 10;
  }

  get usagePercentage(): number {
    if (this.analysesLimit === 0) return 0;
    return Math.min((this.analysesUsed / this.analysesLimit) * 100, 100);
  }

  upgrade(priceId: string): void {
    this.upgrading = true;
    this.error = '';

    const headers = this.getAuthHeaders();
    const body = {
      userId: this.user.id || this.user.Id,
      email: this.user.email || this.user.Email,
      priceId: priceId,
      successUrl: window.location.origin + '/profile?upgraded=true',
      cancelUrl: window.location.origin + '/profile?canceled=true'
    };

    this.http.post<any>(`${environment.apiUrl}/stripe/checkout`, body, { headers })
      .subscribe({
        next: (response) => {
          if (response.success && response.url) {
            window.location.href = response.url;
          } else {
            this.error = 'Failed to start checkout';
            this.upgrading = false;
          }
        },
        error: (err) => {
          console.error('Checkout error', err);
          this.error = 'Failed to start checkout. Please try again.';
          this.upgrading = false;
        }
      });
  }

  private getAuthHeaders(): HttpHeaders {
    const credentials = localStorage.getItem('userCredentials');
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'Authorization': `Basic ${credentials}`
    });
  }
}
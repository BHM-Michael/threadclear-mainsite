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
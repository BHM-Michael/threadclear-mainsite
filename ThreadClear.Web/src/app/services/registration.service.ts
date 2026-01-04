import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface RegistrationRequest {
  email: string;
  password: string;
  firstName?: string;
  lastName?: string;
  organizationName?: string;
  industryType?: string;
  inviteToken?: string;
}

export interface RegistrationResponse {
  success: boolean;
  token?: string;
  user?: any;
  organization?: any;
  error?: string;
}

export interface ValidateInviteResponse {
  success: boolean;
  valid: boolean;
  email?: string;
  organizationName?: string;
  message?: string;
}

export interface AcceptInviteRequest {
  token: string;
  password: string;
  firstName?: string;
  lastName?: string;
}

@Injectable({
  providedIn: 'root'
})
export class RegistrationService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  validateInvite(token: string): Observable<ValidateInviteResponse> {
    return this.http.get<ValidateInviteResponse>(
      `${this.apiUrl}/auth/validate-invite/${token}`
    );
  }

  register(request: RegistrationRequest): Observable<RegistrationResponse> {
    return this.http.post<RegistrationResponse>(
      `${this.apiUrl}/auth/register`,
      request
    ).pipe(
      tap(response => {
        if (response.success && response.token) {
          localStorage.setItem('authToken', response.token);
          if (response.user) {
            localStorage.setItem('currentUser', JSON.stringify(response.user));
          }
          if (response.organization) {
            localStorage.setItem('currentOrgId', response.organization.id);
          }
        }
      })
    );
  }

  acceptInvite(request: AcceptInviteRequest): Observable<RegistrationResponse> {
    return this.http.post<RegistrationResponse>(
      `${this.apiUrl}/auth/accept-invite`,
      request
    ).pipe(
      tap(response => {
        if (response.success && response.token) {
          localStorage.setItem('authToken', response.token);
          if (response.user) {
            localStorage.setItem('currentUser', JSON.stringify(response.user));
          }
          if (response.organization) {
            localStorage.setItem('currentOrgId', response.organization.id);
          }
        }
      })
    );
  }

  verifyEmail(token: string): Observable<{ success: boolean; message: string }> {
    return this.http.get<{ success: boolean; message: string }>(
      `${this.apiUrl}/auth/verify-email/${token}`
    );
  }

  requestPasswordReset(email: string): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(
      `${this.apiUrl}/auth/request-reset`,
      { email }
    );
  }

  resetPassword(token: string, password: string): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(
      `${this.apiUrl}/auth/reset-password`,
      { token, password }
    );
  }
}

import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

export interface Organization {
  id: string;
  name: string;
  slug: string;
  industryType: string;
  plan: string;
  settings?: OrganizationSettings;
  isActive: boolean;
  createdAt?: string;
}

export interface OrganizationSettings {
  allowMemberInvites: boolean;
  requireApproval: boolean;
  storeInsights: boolean;
  insightRetentionDays: number;
  allowedDomains?: string[];
}

export interface OrganizationMember {
  userId: string;
  email: string;
  displayName?: string;
  role: string;
  status: string;
  joinedAt: string;
}

export interface CreateOrganizationRequest {
  name: string;
  industryType?: string;
}

export interface InviteMemberRequest {
  email: string;
  role?: string;
}

@Injectable({
  providedIn: 'root'
})
export class OrganizationService {
  private apiUrl = environment.apiUrl;

  private currentOrgSubject = new BehaviorSubject<Organization | null>(null);
  public currentOrg$ = this.currentOrgSubject.asObservable();

  private userOrgsSubject = new BehaviorSubject<Organization[]>([]);
  public userOrgs$ = this.userOrgsSubject.asObservable();

  constructor(
    private http: HttpClient,
    private authService: AuthService
  ) {
    // Load orgs when user logs in
    this.authService.currentUser$.subscribe(user => {
      // ADD THIS
      if (user) {
        this.loadUserOrganizations();
      } else {
        this.currentOrgSubject.next(null);
        this.userOrgsSubject.next([]);
      }
    });
  }
  get currentOrg(): Organization | null {
    return this.currentOrgSubject.value;
  }

  private getAuthHeaders(): HttpHeaders {
    // Try Bearer token first
    const token = localStorage.getItem('authToken');
    if (token) {
      return new HttpHeaders({
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      });
    }

    // Fall back to email/password credentials
    const credentials = localStorage.getItem('userCredentials');
    if (credentials) {
      const [email, password] = atob(credentials).split(':');
      return new HttpHeaders({
        'X-User-Email': email,
        'X-User-Password': password,
        'Content-Type': 'application/json'
      });
    }

    return new HttpHeaders({
      'Content-Type': 'application/json'
    });
  }

  loadUserOrganizations(): void {

    this.getMyOrganizations().subscribe({
      next: (response) => {

        if (response.success && response.organizations) {
          this.userOrgsSubject.next(response.organizations);

          const savedOrgId = localStorage.getItem('currentOrgId');
          // ADD THIS

          if (savedOrgId) {
            const savedOrg = response.organizations.find(o => o.id === savedOrgId);
            // ADD THIS
            if (savedOrg) {
              // ADD THIS
              this.currentOrgSubject.next(savedOrg);
              return;
            }
          }

          // ADD THIS
          // ADD THIS

          if (!this.currentOrgSubject.value && response.organizations.length > 0) {
            // ADD THIS
            this.setCurrentOrganization(response.organizations[0]);
          }
        }
      },
      error: (err) => console.error('Failed to load organizations', err)
    });
  }

  setCurrentOrganization(org: Organization): void {
    this.currentOrgSubject.next(org);
    localStorage.setItem('currentOrgId', org.id);
  }

  // API calls
  getMyOrganizations(): Observable<{ success: boolean; organizations: Organization[] }> {
    return this.http.get<{ success: boolean; organizations: Organization[] }>(
      `${this.apiUrl}/organizations`,
      { headers: this.getAuthHeaders() }
    );
  }

  getOrganization(orgId: string): Observable<{ success: boolean; organization: Organization; membership: any }> {
    return this.http.get<{ success: boolean; organization: Organization; membership: any }>(
      `${this.apiUrl}/organizations/${orgId}`,
      { headers: this.getAuthHeaders() }
    );
  }

  createOrganization(request: CreateOrganizationRequest): Observable<{ success: boolean; organization: Organization }> {
    return this.http.post<{ success: boolean; organization: Organization }>(
      `${this.apiUrl}/organizations`,
      request,
      { headers: this.getAuthHeaders() }
    ).pipe(
      tap(response => {
        if (response.success) {
          this.loadUserOrganizations();
        }
      })
    );
  }

  updateOrganization(orgId: string, updates: Partial<Organization>): Observable<{ success: boolean; organization: Organization }> {
    return this.http.put<{ success: boolean; organization: Organization }>(
      `${this.apiUrl}/organizations/${orgId}`,
      updates,
      { headers: this.getAuthHeaders() }
    ).pipe(
      tap(response => {
        if (response.success) {
          this.loadUserOrganizations();
        }
      })
    );
  }

  // Members
  getMembers(orgId: string): Observable<{ success: boolean; members: OrganizationMember[] }> {
    return this.http.get<{ success: boolean; members: OrganizationMember[] }>(
      `${this.apiUrl}/organizations/${orgId}/members`,
      { headers: this.getAuthHeaders() }
    );
  }

  inviteMember(orgId: string, request: InviteMemberRequest): Observable<{ success: boolean; inviteToken: string; message: string }> {
    return this.http.post<{ success: boolean; inviteToken: string; message: string }>(
      `${this.apiUrl}/organizations/${orgId}/members/invite`,
      request,
      { headers: this.getAuthHeaders() }
    );
  }

  updateMemberRole(orgId: string, userId: string, role: string): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(
      `${this.apiUrl}/organizations/${orgId}/members/${userId}`,
      { role },
      { headers: this.getAuthHeaders() }
    );
  }

  removeMember(orgId: string, userId: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(
      `${this.apiUrl}/organizations/${orgId}/members/${userId}`,
      { headers: this.getAuthHeaders() }
    );
  }
}

import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AdminOrganization {
  id: string;
  name: string;
  slug: string;
  industryType: string;
  plan: string;
  isActive: boolean;
  createdAt: string;
}

export interface AdminOrgMember {
  userId: string;
  email?: string;
  displayName?: string;
  role: string;
  status: string;
  inviteToken?: string;
  joinedAt?: string;
}

export interface CreateOrgRequest {
  name: string;
  ownerEmail: string;
  industryType?: string;
  plan?: string;
}

export interface CreateOrgResponse {
  success: boolean;
  organization: AdminOrganization;
  owner: {
    id: string;
    email: string;
    isNew: boolean;
  };
  inviteToken?: string;
  inviteUrl?: string;
  error?: string;
}

export interface InviteUserRequest {
  email: string;
  role?: string;
}

export interface InviteResponse {
  success: boolean;
  userId: string;
  email: string;
  role: string;
  inviteToken: string;
  inviteUrl: string;
  error?: string;
}

export interface BulkInviteUser {
  email: string;
  role?: string;
}

export interface BulkInviteResponse {
  success: boolean;
  summary: {
    total: number;
    succeeded: number;
    failed: number;
  };
  results: any[];
  error?: string;
}

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  private getAdminHeaders(): HttpHeaders {
    const credentials = localStorage.getItem('userCredentials');
    if (!credentials) return new HttpHeaders();

    const [email, password] = atob(credentials).split(':');
    return new HttpHeaders({
      'X-Admin-Email': email,
      'X-Admin-Password': password,
      'Content-Type': 'application/json'
    });
  }

  // Organizations
  getOrganizations(): Observable<{ success: boolean; count: number; organizations: AdminOrganization[] }> {
    return this.http.get<{ success: boolean; count: number; organizations: AdminOrganization[] }>(
      `${this.apiUrl}/manage/organizations`,
      { headers: this.getAdminHeaders() }
    );
  }

  getOrganization(orgId: string): Observable<{ success: boolean; organization: AdminOrganization; members: AdminOrgMember[] }> {
    return this.http.get<{ success: boolean; organization: AdminOrganization; members: AdminOrgMember[] }>(
      `${this.apiUrl}/manage/organizations/${orgId}`,
      { headers: this.getAdminHeaders() }
    );
  }

  createOrganization(request: CreateOrgRequest): Observable<CreateOrgResponse> {
    return this.http.post<CreateOrgResponse>(
      `${this.apiUrl}/manage/organizations`,
      request,
      { headers: this.getAdminHeaders() }
    );
  }

  // Invites
  inviteUser(orgId: string, request: InviteUserRequest): Observable<InviteResponse> {
    return this.http.post<InviteResponse>(
      `${this.apiUrl}/manage/organizations/${orgId}/invite`,
      request,
      { headers: this.getAdminHeaders() }
    );
  }

  bulkInvite(orgId: string, users: BulkInviteUser[], defaultRole?: string): Observable<BulkInviteResponse> {
    return this.http.post<BulkInviteResponse>(
      `${this.apiUrl}/manage/organizations/${orgId}/bulk-invite`,
      { users, defaultRole },
      { headers: this.getAdminHeaders() }
    );
  }

  resendInvite(orgId: string, userId: string): Observable<{ success: boolean; email: string; inviteToken: string; inviteUrl: string }> {
    return this.http.post<{ success: boolean; email: string; inviteToken: string; inviteUrl: string }>(
      `${this.apiUrl}/manage/organizations/${orgId}/resend-invite/${userId}`,
      {},
      { headers: this.getAdminHeaders() }
    );
  }
}

import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface TaxonomyData {
  categories: CategoryDefinition[];
  topics: TopicDefinition[];
  roles: RoleDefinition[];
  severityRules: SeverityRule[];
}

export interface CategoryDefinition {
  key: string;
  displayName: string;
  description?: string;
  values: ValueDefinition[];
}

export interface ValueDefinition {
  key: string;
  displayName: string;
  template?: string;
  triggerPatterns?: string[];
}

export interface TopicDefinition {
  key: string;
  displayName: string;
  keywords: string[];
  isCustom?: boolean;
}

export interface RoleDefinition {
  key: string;
  displayName: string;
  keywords: string[];
  emailDomainPatterns?: string[];
}

export interface SeverityRule {
  category: string;
  value: string;
  condition?: string;
  severity: string;
}

export interface IndustryType {
  key: string;
  displayName: string;
}

@Injectable({
  providedIn: 'root'
})
export class TaxonomyService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) { }

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

  // Industry templates (public)
  getIndustryTypes(): Observable<{ success: boolean; industries: IndustryType[] }> {
    return this.http.get<{ success: boolean; industries: IndustryType[] }>(
      `${this.apiUrl}/taxonomy/industries`
    );
  }

  getIndustryTemplate(industryType: string): Observable<{ success: boolean; industryType: string; taxonomy: TaxonomyData }> {
    return this.http.get<{ success: boolean; industryType: string; taxonomy: TaxonomyData }>(
      `${this.apiUrl}/taxonomy/industries/${industryType}`
    );
  }

  // Organization taxonomy
  getOrganizationTaxonomy(orgId: string): Observable<{ success: boolean; taxonomy: TaxonomyData }> {
    return this.http.get<{ success: boolean; taxonomy: TaxonomyData }>(
      `${this.apiUrl}/organizations/${orgId}/taxonomy`,
      { headers: this.getAuthHeaders() }
    );
  }

  updateOrganizationTaxonomy(orgId: string, taxonomy: TaxonomyData): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(
      `${this.apiUrl}/organizations/${orgId}/taxonomy`,
      taxonomy,
      { headers: this.getAuthHeaders() }
    );
  }

  // Topics
  addCustomTopic(orgId: string, topic: TopicDefinition): Observable<{ success: boolean; topic: TopicDefinition; totalTopics: number }> {
    return this.http.post<{ success: boolean; topic: TopicDefinition; totalTopics: number }>(
      `${this.apiUrl}/organizations/${orgId}/taxonomy/topics`,
      topic,
      { headers: this.getAuthHeaders() }
    );
  }

  removeCustomTopic(orgId: string, topicKey: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(
      `${this.apiUrl}/organizations/${orgId}/taxonomy/topics/${topicKey}`,
      { headers: this.getAuthHeaders() }
    );
  }

  // Roles
  addCustomRole(orgId: string, role: RoleDefinition): Observable<{ success: boolean; role: RoleDefinition; totalRoles: number }> {
    return this.http.post<{ success: boolean; role: RoleDefinition; totalRoles: number }>(
      `${this.apiUrl}/organizations/${orgId}/taxonomy/roles`,
      role,
      { headers: this.getAuthHeaders() }
    );
  }

  removeCustomRole(orgId: string, roleKey: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(
      `${this.apiUrl}/organizations/${orgId}/taxonomy/roles/${roleKey}`,
      { headers: this.getAuthHeaders() }
    );
  }
}

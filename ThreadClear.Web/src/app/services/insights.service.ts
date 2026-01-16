import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface InsightSummary {
  TotalConversations: number;
  HighRiskCount: number;
  MediumRiskCount: number;
  LowRiskCount: number;
  AverageHealthScore: number;
  TotalInsightEntries: number;
  ByCategory: { [key: string]: number };
  BySourceType: { [key: string]: number };
}

export interface InsightTrend {
  Period: string;
  ConversationCount: number;
  HighRiskCount: number;
  AverageHealthScore: number;
}

export interface TopicBreakdown {
  Topic: string;
  Count: number;
  HighSeverityCount: number;
  ByCategory: { [key: string]: number };
}

export interface StorableInsight {
  id: string;
  organizationId: string;
  userId?: string;
  timestamp: string;
  sourceType: string;
  participantCount: number;
  messageCount: number;
  overallRisk: string;
  healthScore: number;
  insightCount: number;
  insights: InsightEntry[];
}

export interface InsightEntry {
  category: string;
  value: string;
  role: string;
  topic: string;
  severity: string;
}

@Injectable({
  providedIn: 'root'
})
export class InsightsService {
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

  // Dashboard
  getDashboardSummary(orgId: string, days: number = 30): Observable<{ success: boolean; days: number; summary: InsightSummary }> {
    return this.http.get<{ success: boolean; days: number; summary: InsightSummary }>(
      `${this.apiUrl}/organizations/${orgId}/insights/summary?days=${days}`,
      { headers: this.getAuthHeaders() }
    );
  }

  getTrends(orgId: string, days: number = 30, groupBy: string = 'day'): Observable<{ success: boolean; days: number; groupBy: string; trends: InsightTrend[] }> {
    return this.http.get<{ success: boolean; days: number; groupBy: string; trends: InsightTrend[] }>(
      `${this.apiUrl}/organizations/${orgId}/insights/trends?days=${days}&groupBy=${groupBy}`,
      { headers: this.getAuthHeaders() }
    );
  }

  getTopicAnalysis(orgId: string, days: number = 30): Observable<{ success: boolean; days: number; topics: TopicBreakdown[] }> {
    return this.http.get<{ success: boolean; days: number; topics: TopicBreakdown[] }>(
      `${this.apiUrl}/organizations/${orgId}/insights/topics?days=${days}`,
      { headers: this.getAuthHeaders() }
    );
  }

  // Insights list
  getRecentInsights(orgId: string, limit: number = 50): Observable<{ success: boolean; count: number; insights: StorableInsight[] }> {
    return this.http.get<{ success: boolean; count: number; insights: StorableInsight[] }>(
      `${this.apiUrl}/organizations/${orgId}/insights?limit=${limit}`,
      { headers: this.getAuthHeaders() }
    );
  }

  getInsightDetail(insightId: string): Observable<{ success: boolean; insight: StorableInsight }> {
    return this.http.get<{ success: boolean; insight: StorableInsight }>(
      `${this.apiUrl}/insights/${insightId}`,
      { headers: this.getAuthHeaders() }
    );
  }

  getMyInsights(limit: number = 50): Observable<{ success: boolean; count: number; insights: StorableInsight[] }> {
    return this.http.get<{ success: boolean; count: number; insights: StorableInsight[] }>(
      `${this.apiUrl}/insights/mine?limit=${limit}`,
      { headers: this.getAuthHeaders() }
    );
  }
}

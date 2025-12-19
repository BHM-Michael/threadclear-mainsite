import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface AnalysisRequest {
  conversationText: string;
  sourceType: string;
  parsingMode?: number;
}

export interface AnalysisResponse {
  success: boolean;
  capsule: any;
}

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  //private apiUrl = 'http://localhost:7083/api';
  private apiUrl = 'https://threadclear-functions-fsewbzcsd8fuegdj.eastus-01.azurewebsites.net/api';
  private functionKey = '';

  constructor(private http: HttpClient) { }

  analyzeConversation(request: AnalysisRequest): Observable<AnalysisResponse> {
    return this.http.post<AnalysisResponse>(
      `${this.apiUrl}/analyze?code=${this.functionKey}`,
      request
    );
  }

  healthCheck(): Observable<any> {
    return this.http.get(`${this.apiUrl}/health`);
  }

  detectSourceType(text: string): string {
    // Email detection
    if (/^(From|To|Cc|Subject|Date):/mi.test(text)) {
      return 'email';
    }

    // Slack detection
    if (/\w+\s+\[\d{1,2}:\d{2}\s*(?:AM|PM)?\]:/m.test(text)) {
      return 'slack';
    }

    // Teams detection (similar to Slack)
    if (/\[\d{1,2}:\d{2}\s*(?:AM|PM)?\]\s+\w+:/m.test(text)) {
      return 'teams';
    }

    // Default to simple
    return 'simple';
  }
}
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AnalysisRequest {
  conversationText: string;
  sourceType: string;
  parsingMode?: number;
}

export interface AnalysisResponse {
  success: boolean;
  capsule: any;
  parsingMode: string;
}

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private apiUrl = environment.apiUrl;
  private functionKey = environment.functionKey;

  constructor(private http: HttpClient) { }

  analyzeConversation(request: AnalysisRequest): Observable<AnalysisResponse> {
    const url = this.functionKey
      ? `${this.apiUrl}/analyze?code=${this.functionKey}`
      : `${this.apiUrl}/analyze`;
    return this.http.post<AnalysisResponse>(url, request);
  }

  analyzeImage(file: File, sourceType: string, parsingMode: number): Observable<AnalysisResponse> {
    const formData = new FormData();
    formData.append('image', file);
    formData.append('sourceType', sourceType);
    formData.append('parsingMode', parsingMode.toString());

    const url = this.functionKey
      ? `${this.apiUrl}/analyze-image?code=${this.functionKey}`
      : `${this.apiUrl}/analyze-image`;

    return this.http.post<AnalysisResponse>(url, formData);
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
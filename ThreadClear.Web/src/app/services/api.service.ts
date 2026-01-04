import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AnalysisRequest {
  conversationText: string;
  sourceType: string;
  parsingMode?: number;
  draftMessage?: string;
}

export interface AnalysisResponse {
  success: boolean;
  capsule: any;
  parsingMode: string;
  draftAnalysis?: any;
}

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private apiUrl = environment.apiUrl;
  private functionKey = environment.functionKey;

  constructor(private http: HttpClient) { }

  private getAuthHeaders(): HttpHeaders {
    const credentials = localStorage.getItem('userCredentials');
    if (!credentials) {
      return new HttpHeaders({
        'Content-Type': 'application/json'
      });
    }

    const [email, password] = atob(credentials).split(':');
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'X-User-Email': email,
      'X-User-Password': password
    });
  }

  analyzeConversation(data: any): Observable<AnalysisResponse> {
    const url = this.functionKey
      ? `${this.apiUrl}/analyze?code=${this.functionKey}`
      : `${this.apiUrl}/analyze`;

    return this.http.post<AnalysisResponse>(url, data, { headers: this.getAuthHeaders() });
  }

  analyzeImage(file: File, sourceType: string, parsingMode: number): Observable<AnalysisResponse> {
    const formData = new FormData();
    formData.append('image', file);
    formData.append('sourceType', sourceType);
    formData.append('parsingMode', parsingMode.toString());

    const url = this.functionKey
      ? `${this.apiUrl}/analyze-image?code=${this.functionKey}`
      : `${this.apiUrl}/analyze-image`;

    // For FormData, we need different headers (no Content-Type, browser sets it with boundary)
    const headers = this.getFormDataAuthHeaders();
    return this.http.post<AnalysisResponse>(url, formData, { headers });
  }

  analyzeImages(files: File[], sourceType: string, parsingMode: number, enableFlags: any = {}): Observable<AnalysisResponse> {
    const formData = new FormData();
    files.forEach((file, index) => {
      formData.append('images', file);
    });
    formData.append('sourceType', sourceType);
    formData.append('parsingMode', parsingMode.toString());

    // Add permission flags
    if (Object.keys(enableFlags).length > 0) {
      formData.append('enableFlags', JSON.stringify(enableFlags));
    }

    const url = this.functionKey
      ? `${this.apiUrl}/analyze-images?code=${this.functionKey}`
      : `${this.apiUrl}/analyze-images`;

    const headers = this.getFormDataAuthHeaders();
    return this.http.post<AnalysisResponse>(url, formData, { headers });
  }

  analyzeAudio(file: File, sourceType: string, parsingMode: number, enableFlags: any = {}): Observable<AnalysisResponse> {
    const formData = new FormData();
    formData.append('audio', file);
    formData.append('sourceType', sourceType);
    formData.append('parsingMode', parsingMode.toString());

    // Add permission flags
    if (Object.keys(enableFlags).length > 0) {
      formData.append('enableFlags', JSON.stringify(enableFlags));
    }

    const url = this.functionKey
      ? `${this.apiUrl}/analyze-audio?code=${this.functionKey}`
      : `${this.apiUrl}/analyze-audio`;

    const headers = this.getFormDataAuthHeaders();
    return this.http.post<AnalysisResponse>(url, formData, { headers });
  }

  private getFormDataAuthHeaders(): HttpHeaders {
    const credentials = localStorage.getItem('userCredentials');
    if (!credentials) {
      return new HttpHeaders();
    }

    const [email, password] = atob(credentials).split(':');
    return new HttpHeaders({
      'X-User-Email': email,
      'X-User-Password': password
    });
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
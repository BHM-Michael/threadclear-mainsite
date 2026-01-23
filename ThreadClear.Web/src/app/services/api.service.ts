import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, Subject } from 'rxjs';
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

export interface StreamEvent {
  status: 'started' | 'streaming' | 'complete' | 'error';
  message?: string;
  chunk?: string;
  totalLength?: number;
  capsule?: any;
  user?: string;
}

export interface QuickParseResponse {
  success: boolean;
  participants: any[];
  messages: any[];
  sourceType: string;
  metadata: any;
  parseTimeMs: number;
}

export interface SectionResponse {
  success: boolean;
  section: string;
  data: any;
  timeMs: number;
  error?: string;
}

export interface ExtractTextResponse {
  success: boolean;
  text: string;
  imageCount: number;
  extractTimeMs: number;
  error?: string;
}

export interface SpellCheckIssue {
  word: string;
  startIndex: number;
  endIndex: number;
  type: 'spelling' | 'grammar' | 'typo';
  message: string;
  suggestions: string[];
  severity?: string;
}

export interface MessageSpellCheckResult {
  messageId: string;
  issues: SpellCheckIssue[];
}

export interface SpellCheckResponse {
  success: boolean;
  results: MessageSpellCheckResult[];
  totalIssues: number;
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

  // Quick regex-based parsing (no AI) - returns in ~1-2 seconds
  quickParse(conversationText: string): Observable<QuickParseResponse> {
    const url = this.functionKey
      ? `${this.apiUrl}/parse/quick?code=${this.functionKey}`
      : `${this.apiUrl}/parse/quick`;

    return this.http.post<QuickParseResponse>(url, { conversationText }, { headers: this.getAuthHeaders() });
  }

  // Analyze a single section (AI-powered) - call in parallel for each section
  analyzeSection(conversationText: string, section: string): Observable<SectionResponse> {
    const url = this.functionKey
      ? `${this.apiUrl}/analyze/section?code=${this.functionKey}`
      : `${this.apiUrl}/analyze/section`;

    return this.http.post<SectionResponse>(url, { conversationText, section }, { headers: this.getAuthHeaders() });
  }

  // Full analysis (original endpoint) - still available as fallback
  analyzeConversation(data: any): Observable<AnalysisResponse> {
    const url = this.functionKey
      ? `${this.apiUrl}/analyze?code=${this.functionKey}`
      : `${this.apiUrl}/analyze`;

    return this.http.post<AnalysisResponse>(url, data, { headers: this.getAuthHeaders() });
  }

  // Streaming analysis (not currently working due to Azure Functions buffering)
  analyzeConversationStream(conversationText: string, sourceType: string = 'simple'): Observable<StreamEvent> {
    const subject = new Subject<StreamEvent>();

    const credentials = localStorage.getItem('userCredentials');
    const headers: Record<string, string> = {
      'Content-Type': 'application/json'
    };

    if (credentials) {
      const [email, password] = atob(credentials).split(':');
      headers['X-User-Email'] = email;
      headers['X-User-Password'] = password;
    }

    fetch(`${this.apiUrl}/analyze/stream`, {
      method: 'POST',
      headers,
      body: JSON.stringify({ conversationText, sourceType })
    })
      .then(response => {
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }

        const reader = response.body?.getReader();
        const decoder = new TextDecoder();
        let buffer = '';

        const read = (): void => {
          reader?.read().then(({ done, value }) => {
            if (done) {
              subject.complete();
              return;
            }

            buffer += decoder.decode(value, { stream: true });
            const lines = buffer.split('\n\n');
            buffer = lines.pop() || '';

            for (const line of lines) {
              if (line.startsWith('data: ')) {
                try {
                  const data = JSON.parse(line.substring(6)) as StreamEvent;
                  subject.next(data);
                } catch (e) {
                  console.warn('Failed to parse stream event:', e);
                }
              }
            }

            read();
          }).catch(error => {
            subject.error(error);
          });
        };

        read();
      })
      .catch(error => {
        subject.error(error);
      });

    return subject.asObservable();
  }

  analyzeImage(file: File, sourceType: string, parsingMode: number): Observable<AnalysisResponse> {
    const formData = new FormData();
    formData.append('image', file);
    formData.append('sourceType', sourceType);
    formData.append('parsingMode', parsingMode.toString());

    const url = this.functionKey
      ? `${this.apiUrl}/analyze-image?code=${this.functionKey}`
      : `${this.apiUrl}/analyze-image`;

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

  extractTextFromImages(files: File[]): Observable<ExtractTextResponse> {
    const formData = new FormData();
    files.forEach(file => {
      formData.append('images', file);
    });

    const url = this.functionKey
      ? `${this.apiUrl}/images/extract-text?code=${this.functionKey}`
      : `${this.apiUrl}/images/extract-text`;

    const headers = this.getFormDataAuthHeaders();
    return this.http.post<ExtractTextResponse>(url, formData, { headers });
  }

  // Store insight after progressive analysis completes
  storeInsight(capsule: any): Observable<{ success: boolean; stored: boolean; reason?: string }> {
    const url = this.functionKey
      ? `${this.apiUrl}/insights/store?code=${this.functionKey}`
      : `${this.apiUrl}/insights/store`;




    return this.http.post<{ success: boolean; stored: boolean; reason?: string }>(
      url,
      { capsule },
      { headers: this.getAuthHeaders() }
    );
  }

  // Check spelling/grammar for multiple messages
  checkMessagesSpelling(messages: { messageId: string; text: string }[]): Observable<SpellCheckResponse> {
    const url = this.functionKey
      ? `${this.apiUrl}/spellcheck/messages?code=${this.functionKey}`
      : `${this.apiUrl}/spellcheck/messages`;

    return this.http.post<SpellCheckResponse>(
      url,
      { messages },
      { headers: this.getAuthHeaders() }
    );
  }

  // Check spelling/grammar for single text
  checkTextSpelling(text: string): Observable<{ success: boolean; issues: SpellCheckIssue[]; totalIssues: number }> {
    const url = this.functionKey
      ? `${this.apiUrl}/spellcheck/text?code=${this.functionKey}`
      : `${this.apiUrl}/spellcheck/text`;

    return this.http.post<{ success: boolean; issues: SpellCheckIssue[]; totalIssues: number }>(
      url,
      { text },
      { headers: this.getAuthHeaders() }
    );
  }

  healthCheck(): Observable<any> {
    return this.http.get(`${this.apiUrl}/health`);
  }

  detectSourceType(text: string): string {
    if (/^(From|To|Cc|Subject|Date):/mi.test(text)) {
      return 'email';
    }
    if (/\w+\s+\[\d{1,2}:\d{2}\s*(?:AM|PM)?\]:/m.test(text)) {
      return 'slack';
    }
    if (/\[\d{1,2}:\d{2}\s*(?:AM|PM)?\]\s+\w+:/m.test(text)) {
      return 'teams';
    }
    return 'simple';
  }
}
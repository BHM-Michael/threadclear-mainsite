import { Component } from '@angular/core';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-conversation-analyzer',
  templateUrl: './conversation-analyzer.component.html',
  styleUrls: ['./conversation-analyzer.component.scss']
})
export class ConversationAnalyzerComponent {
  conversationText = '';
  sourceType = 'auto';
  parsingMode: number | null = 2; // Keep as number
  loading = false;
  results: any = null;
  error = '';

  constructor(private apiService: ApiService) { }

  analyze() {
    this.loading = true;
    this.error = '';
    this.results = null;

    const request: any = {
      conversationText: this.conversationText,
      sourceType: this.sourceType
    };

    // Only add parsingMode if it's a valid number
    if (this.parsingMode !== null && !isNaN(this.parsingMode)) {
      request.parsingMode = this.parsingMode;
    }

    this.apiService.analyzeConversation(request).subscribe({
      next: (response) => {
        this.results = response.capsule;
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Error analyzing conversation. Make sure your API is running.';
        this.loading = false;
        console.error(err);
      }
    });
  }
}
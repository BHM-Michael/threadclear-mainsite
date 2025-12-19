import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-results-display',
  templateUrl: './results-display.component.html',
  styleUrls: ['./results-display.component.scss']
})
export class ResultsDisplayComponent {
  @Input() results: any;

  // Participant helpers
  getParticipantName(id: string): string {
    if (!this.results?.Participants) return id;
    const participant = this.results.Participants.find((p: any) => p.Id === id);
    return participant ? participant.Name : id;
  }

  getRoleName(role: number): string {
    const roles: { [key: number]: string } = {
      0: 'Unknown',
      1: 'Sender',
      2: 'Recipient',
      3: 'Moderator',
      4: 'Observer'
    };
    return roles[role] || 'Unknown';
  }

  // Styling helpers
  getUrgencyClass(urgency: string): string {
    return urgency ? urgency.toLowerCase() : 'low';
  }

  getSeverityClass(severity: string): string {
    return severity ? severity.toLowerCase() : 'low';
  }

  getRiskClass(risk: string): string {
    return risk ? risk.toLowerCase() : 'low';
  }

  getSentimentIcon(sentiment: string): string {
    const icons: { [key: string]: string } = {
      'Positive': '😊',
      'Negative': '😟',
      'Neutral': '😐'
    };
    return icons[sentiment] || '😐';
  }

  // Message helpers
  getQuestionsFromMessage(message: any): string[] {
    return message?.LinguisticFeatures?.Questions || [];
  }

  hasQuestions(message: any): boolean {
    return message?.LinguisticFeatures?.ContainsQuestion || false;
  }

  // Analysis helpers
  hasAnalysis(): boolean {
    return !!this.results?.Analysis;
  }

  hasUnansweredQuestions(): boolean {
    return this.results?.Analysis?.UnansweredQuestions?.length > 0;
  }

  hasTensionPoints(): boolean {
    return this.results?.Analysis?.TensionPoints?.length > 0;
  }

  hasMisalignments(): boolean {
    return this.results?.Analysis?.Misalignments?.length > 0;
  }

  hasDecisions(): boolean {
    return this.results?.Analysis?.Decisions?.some((d: any) => d.Decision && d.Decision.trim() !== '');
  }

  hasActionItems(): boolean {
    return this.results?.Analysis?.ActionItems?.length > 0;
  }

  hasSuggestedActions(): boolean {
    return this.results?.SuggestedActions?.length > 0;
  }

  hasConversationHealth(): boolean {
    return !!this.results?.Analysis?.ConversationHealth;
  }

  // Score formatting
  formatScore(score: number): number {
    return Math.round(score * 100);
  }

  getScoreClass(score: number): string {
    if (score >= 0.7) return 'good';
    if (score >= 0.4) return 'medium';
    return 'poor';
  }

  // Metadata helpers
  getMetadataValue(key: string): string {
    return this.results?.Metadata?.[key] || '';
  }

  parseParticipantActivity(): { name: string; count: number }[] {
    const activityJson = this.results?.Metadata?.ParticipantActivity;
    if (!activityJson) return [];

    try {
      const activity = JSON.parse(activityJson);
      return Object.entries(activity).map(([id, count]) => ({
        name: this.getParticipantName(id),
        count: count as number
      }));
    } catch {
      return [];
    }
  }
}

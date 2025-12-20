import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-results-display',
  templateUrl: './results-display.component.html',
  styleUrls: ['./results-display.component.scss']
})
export class ResultsDisplayComponent {
  @Input() results: any;

  participantsExpanded = false;
  messagesExpanded = false;

  // Scroll to section
  // Scroll to section and expand if needed
  scrollTo(sectionId: string): void {
    if (sectionId === 'participants') {
      this.participantsExpanded = true;
    } else if (sectionId === 'messages') {
      this.messagesExpanded = true;
    }

    // Small delay to allow expansion before scrolling
    setTimeout(() => {
      const element = document.getElementById(sectionId);
      if (element) {
        element.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }
    }, 100);
  }

  // Toggle section
  toggleSection(section: string): void {
    if (section === 'participants') {
      this.participantsExpanded = !this.participantsExpanded;
    } else if (section === 'messages') {
      this.messagesExpanded = !this.messagesExpanded;
    }
  }

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

  formatPoliteness(score: number): number {
    if (!score) return 50;
    return Math.round(score * 100);
  }

  getIssuesForMessage(message: any, index: number): string[] {
    const issues: string[] = [];

    if (!message) return issues;

    const messageId = message?.Id || `msg-${index}`;

    // Check tension points
    if (this.results?.Analysis?.TensionPoints) {
      for (const tp of this.results.Analysis.TensionPoints) {
        if (tp.MessageId === messageId || tp.MessageId === index.toString()) {
          issues.push(`Tension: ${tp.Description}`);
        }
      }
    }

    // Check misalignments
    if (this.results?.Analysis?.Misalignments) {
      for (const ma of this.results.Analysis.Misalignments) {
        if (ma.MessageId === messageId || ma.MessageId === index.toString()) {
          issues.push(`Misalignment: ${ma.Description}`);
        }
      }
    }

    return issues;
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
    if (!score) return 0;
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
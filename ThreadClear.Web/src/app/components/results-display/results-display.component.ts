import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-results-display',
  templateUrl: './results-display.component.html',
  styleUrls: ['./results-display.component.scss']
})
export class ResultsDisplayComponent {
  @Input() results: any;

  // NEW: Section loading states from parent component
  @Input() sectionsLoading: { [key: string]: boolean } = {
    summary: false,
    questions: false,
    tensions: false,
    health: false,
    actions: false,
    misalignments: false
  };

  @Input() sectionsComplete: { [key: string]: boolean } = {
    summary: false,
    questions: false,
    tensions: false,
    health: false,
    actions: false,
    misalignments: false
  };

  @Input() sectionsError: { [key: string]: string } = {};
  // Collapsible sections state
  participantsExpanded = true;
  messagesExpanded = true;

  isSectionLoading(section: string): boolean {
    return this.sectionsLoading && this.sectionsLoading[section] === true;
  }

  isSectionComplete(section: string): boolean {
    return this.sectionsComplete && this.sectionsComplete[section] === true;
  }

  toggleSection(section: string) {
    if (section === 'participants') {
      this.participantsExpanded = !this.participantsExpanded;
    } else if (section === 'messages') {
      this.messagesExpanded = !this.messagesExpanded;
    }
  }

  scrollTo(elementId: string) {
    const element = document.getElementById(elementId);
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  getParticipantName(participantId: string): string {
    if (!this.results?.Participants || !participantId) return participantId || 'Unknown';
    const participant = this.results.Participants.find((p: any) =>
      p.Id === participantId || p.Name === participantId
    );
    return participant?.Name || participantId;
  }

  getRoleName(role: number | undefined): string {
    if (role === undefined || role === null) return 'Unknown';
    const roles: { [key: number]: string } = {
      0: 'Unknown',
      1: 'Initiator',
      2: 'Responder',
      3: 'Observer',
      4: 'Moderator'
    };
    return roles[role] || 'Unknown';
  }

  getMetadataValue(key: string): any {
    if (!this.results?.Metadata) return '';
    return this.results.Metadata[key] || this.results.Metadata[key.toLowerCase()] || '';
  }

  // Conversation Health helpers
  hasConversationHealth(): boolean {
    const health = this.results?.Analysis?.ConversationHealth;
    return health && (health.OverallScore !== undefined || health.RiskLevel);
  }

  getHealthClass(score: number | undefined): string {
    if (score === undefined) return '';
    if (score >= 70) return 'good';
    if (score >= 40) return 'warning';
    return 'danger';
  }

  getScoreClass(score: number | undefined): string {
    if (score === undefined) return '';
    if (score >= 70) return 'good';
    if (score >= 40) return 'warning';
    return 'danger';
  }

  formatScore(score: number | undefined): number {
    if (score === undefined) return 0;
    return Math.round(score);
  }

  // Unanswered Questions helpers
  hasUnansweredQuestions(): boolean {
    const questions = this.results?.Analysis?.UnansweredQuestions;
    return questions && questions.length > 0;
  }

  // Tension Points helpers
  hasTensionPoints(): boolean {
    const tensions = this.results?.Analysis?.TensionPoints;
    return tensions && tensions.length > 0;
  }

  getSeverityClass(severity: string | undefined): string {
    if (!severity) return 'low';
    return severity.toLowerCase();
  }

  // Misalignments helpers
  hasMisalignments(): boolean {
    const misalignments = this.results?.Analysis?.Misalignments;
    return misalignments && misalignments.length > 0;
  }

  // Suggested Actions helpers
  hasSuggestedActions(): boolean {
    const actions = this.results?.SuggestedActions;
    return actions && actions.length > 0;
  }

  // Message helpers
  getSentimentIcon(sentiment: string | undefined): string {
    if (!sentiment) return '😐';
    const lower = sentiment.toLowerCase();
    if (lower.includes('positive')) return '😊';
    if (lower.includes('negative')) return '😟';
    if (lower.includes('frustrated')) return '😤';
    if (lower.includes('neutral')) return '😐';
    return '😐';
  }

  getUrgencyClass(urgency: string | undefined): string {
    if (!urgency) return 'normal';
    return urgency.toLowerCase();
  }

  hasQuestions(message: any): boolean {
    return message?.Content?.includes('?') ||
      (message?.LinguisticFeatures?.Questions?.length > 0);
  }

  formatPoliteness(politeness: number | undefined): number {
    if (politeness === undefined) return 0;
    return Math.round(politeness * 100);
  }

  getQuestionsFromMessage(message: any): string[] {
    if (message?.LinguisticFeatures?.Questions) {
      return message.LinguisticFeatures.Questions;
    }
    // Fallback: extract questions from content
    const content = message?.Content || '';
    const questions = content.match(/[^.!?]*\?/g) || [];
    return questions.map((q: string) => q.trim()).filter((q: string) => q.length > 3);
  }

  getIssuesForMessage(message: any, index: number): string[] {
    const issues: string[] = [];
    const messageId = message?.Id || `msg${index + 1}`;

    // Check tension points
    if (this.results?.Analysis?.TensionPoints) {
      this.results.Analysis.TensionPoints.forEach((tp: any) => {
        if (tp.MessageId === messageId) {
          issues.push(`Tension: ${tp.Description}`);
        }
      });
    }

    // Check misalignments
    if (this.results?.Analysis?.Misalignments) {
      this.results.Analysis.Misalignments.forEach((ma: any) => {
        if (ma.MessageId === messageId) {
          issues.push(`Misalignment: ${ma.Description}`);
        }
      });
    }

    return issues;
  }

  // Participant Activity
  parseParticipantActivity(): { name: string; count: number }[] {
    if (!this.results?.Messages || !this.results?.Participants) return [];

    const counts: { [key: string]: number } = {};

    this.results.Messages.forEach((m: any) => {
      const name = this.getParticipantName(m.ParticipantId || m.Sender);
      counts[name] = (counts[name] || 0) + 1;
    });

    return Object.entries(counts)
      .map(([name, count]) => ({ name, count }))
      .sort((a, b) => b.count - a.count);
  }
}
import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { OrganizationService, Organization } from '../../services/organization.service';
import { InsightsService, InsightSummary, InsightTrend, TopicBreakdown } from '../../services/insights.service';
import { TrendChartComponent } from '../trend-chart/trend-chart.component';

export interface NeedsAttentionItem {
  Id: string;
  Subject: string;
  HealthScore: number;
  OverallRisk: string;
  Timestamp: string;
  SourceType: string;
  UnansweredQuestionsCount: number;
  TensionPointsCount: number;
}

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  currentOrg: Organization | null = null;
  summary: InsightSummary | null = null;
  trends: InsightTrend[] = [];
  topics: TopicBreakdown[] = [];
  needsAttention: NeedsAttentionItem[] = [];

  // Drill down
  drillDownDate: string | null = null;
  drillDownInsights: any[] = [];
  drillDownLoading = false;

  selectedDays = 30;
  isLoading = true;
  error: string | null = null;

  selectedConversations: any[] = [];
  selectedDate: string | null = null;

  // Human-readable labels for source types
  private sourceLabels: Record<string, string> = {
    'email': '📧 Email',
    'Email': '📧 Email',
    'slack': '💬 Slack',
    'Slack': '💬 Slack',
    'teams': '🟦 Teams',
    'Teams': '🟦 Teams',
    'sms': '📱 SMS',
    'SMS': '📱 SMS',
    'paste': '📋 Pasted Text',
    'Paste': '📋 Pasted Text',
    'image': '🖼️ Image Upload',
    'Image': '🖼️ Image Upload',
    'audio': '🎙️ Audio',
    'Audio': '🎙️ Audio',
  };

  // Human-readable labels for topic categories
  private categoryLabels: Record<string, string> = {
    'QUESTION_STATUS': 'Unanswered Questions',
    'TENSION_SIGNAL': 'Tension',
    'MISALIGNMENT': 'Misalignment',
    'ACTION_ITEM': 'Action Items',
    'COMMITMENT': 'Commitments',
    'DECISION': 'Decisions',
    'RISK': 'Risk',
    'ESCALATION': 'Escalation',
  };

  constructor(
    private orgService: OrganizationService,
    private insightsService: InsightsService
  ) { }

  ngOnInit(): void {
    this.orgService.currentOrg$
      .pipe(takeUntil(this.destroy$))
      .subscribe(org => {
        this.currentOrg = org;
        if (org) {
          this.loadDashboardData();
        } else {
          this.isLoading = false;
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadDashboardData(): void {
    if (!this.currentOrg) return;

    this.isLoading = true;
    this.error = null;

    const orgId = this.currentOrg.id;

    // Load summary
    this.insightsService.getDashboardSummary(orgId, this.selectedDays).subscribe({
      next: (response) => {
        if (response.success) {
          this.summary = response.summary;
        }
      },
      error: (err) => {
        this.error = 'Failed to load summary';
        console.error(err);
      }
    });

    // Load trends
    this.insightsService.getTrends(orgId, this.selectedDays, 'day').subscribe({
      next: (response) => {
        if (response.success) {
          this.trends = response.trends || [];
        }
      },
      error: (err) => console.error('Failed to load trends', err)
    });

    // Load topics
    this.insightsService.getTopicAnalysis(orgId, this.selectedDays).subscribe({
      next: (response) => {
        if (response.success) {
          this.topics = response.topics || [];
        }
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Failed to load topics', err);
        this.isLoading = false;
      }
    });

    // Load needs attention (low health score conversations)
    this.insightsService.getNeedsAttention(orgId, this.selectedDays).subscribe({
      next: (response: { success: boolean; items: NeedsAttentionItem[] }) => {
        if (response.success) {
          this.needsAttention = response.items || [];
        }
      },
      error: (err: any) => console.error('Failed to load needs attention', err)
    });
  }

  onDaysChange(days: number): void {
    this.selectedDays = days;
    this.loadDashboardData();
  }

  getRiskClass(risk: string): string {
    switch (risk?.toLowerCase()) {
      case 'high': return 'risk-high';
      case 'medium': return 'risk-medium';
      case 'low': return 'risk-low';
      default: return '';
    }
  }

  getHealthScoreClass(score: number): string {
    if (score >= 70) return 'health-good';
    if (score >= 40) return 'health-medium';
    return 'health-poor';
  }

  getMaxConversations(): number {
    if (this.trends.length === 0) return 1;
    return Math.max(...this.trends.map(t => t.ConversationCount || (t as any).ConversationCount || 0), 1);
  }

  getSourceTypes(): { key: string; value: number }[] {
    const sourceData = this.summary?.BySourceType || (this.summary as any)?.bySourceType;
    if (!sourceData) return [];
    return Object.entries(sourceData).map(([key, value]) => ({ key, value: value as number }));
  }

  getSourcePercentage(count: number): number {
    if (!this.summary) return 0;
    const total = this.summary.TotalConversations || (this.summary as any)?.totalConversations || 1;
    return (count / total) * 100;
  }

  getSourceLabel(key: string): string {
    return this.sourceLabels[key] || key;
  }

  getTopCategories(topic: TopicBreakdown): { key: string; value: number }[] {
    const categoryData = topic.ByCategory || (topic as any)?.ByCategory;
    if (!categoryData) return [];
    return Object.entries(categoryData)
      .map(([key, value]) => ({ key, value: value as number }))
      .slice(0, 3);
  }

  getCategoryLabel(key: string): string {
    return this.categoryLabels[key] || key;
  }

  getTopicSeverity(topic: TopicBreakdown): string {
    if (topic.HighSeverityCount > 0) return 'high';
    return 'low';
  }

  onChartDateClick(date: string): void {
    if (!this.currentOrg) return;
    this.drillDownDate = date;
    this.selectedDate = date;
    this.drillDownLoading = true;
    this.drillDownInsights = [];
    this.selectedConversations = [];

    this.insightsService.getInsightsByDate(this.currentOrg.id, date).subscribe({
      next: (response) => {
        if (response.success) {
          this.drillDownInsights = response.insights || [];
          this.selectedConversations = this.drillDownInsights; // feed the panel
        }
        this.drillDownLoading = false;
      },
      error: (err) => {
        console.error('Failed to load drill down', err);
        this.drillDownLoading = false;
      }
    });
  }

  closeDrillDown(): void {
    this.drillDownDate = null;
    this.drillDownInsights = [];
  }
}

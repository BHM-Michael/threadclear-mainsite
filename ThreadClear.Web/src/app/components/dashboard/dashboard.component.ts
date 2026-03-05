import { Component, OnInit, OnDestroy, AfterViewInit, ViewChild, ElementRef } from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
import { Chart, registerables } from 'chart.js';
import { OrganizationService, Organization } from '../../services/organization.service';
import { InsightsService, InsightSummary, InsightTrend, TopicBreakdown } from '../../services/insights.service';

Chart.register(...registerables);

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
export class DashboardComponent implements OnInit, OnDestroy, AfterViewInit {
  @ViewChild('trendChart') trendChartRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('gaugeChart') gaugeChartRef!: ElementRef<HTMLCanvasElement>;

  private destroy$ = new Subject<void>();
  private trendChartInstance: Chart | null = null;
  private gaugeChartInstance: Chart | null = null;
  private viewInitialized = false;
  private dataLoaded = false;

  currentOrg: Organization | null = null;
  summary: InsightSummary | null = null;
  trends: InsightTrend[] = [];
  topics: TopicBreakdown[] = [];
  needsAttention: NeedsAttentionItem[] = [];

  selectedDays = 30;
  isLoading = true;
  error: string | null = null;

  private sourceLabels: Record<string, string> = {
    'email': '📧 Email', 'Email': '📧 Email',
    'slack': '💬 Slack', 'Slack': '💬 Slack',
    'teams': '🟦 Teams', 'Teams': '🟦 Teams',
    'sms': '📱 SMS', 'SMS': '📱 SMS',
    'paste': '📋 Pasted Text', 'Paste': '📋 Pasted Text',
    'image': '🖼️ Image Upload', 'Image': '🖼️ Image Upload',
    'audio': '🎙️ Audio', 'Audio': '🎙️ Audio',
  };

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

  ngAfterViewInit(): void {
    this.viewInitialized = true;
    if (this.dataLoaded) this.renderCharts();
  }

  ngOnDestroy(): void {
    this.trendChartInstance?.destroy();
    this.gaugeChartInstance?.destroy();
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadDashboardData(): void {
    if (!this.currentOrg) return;
    this.isLoading = true;
    this.error = null;
    const orgId = this.currentOrg.id;

    this.insightsService.getDashboardSummary(orgId, this.selectedDays).subscribe({
      next: (response) => {
        if (response.success) {
          this.summary = response.summary;
          if (this.viewInitialized) this.renderGauge();
        }
      },
      error: (err) => { this.error = 'Failed to load summary'; console.error(err); }
    });

    this.insightsService.getTrends(orgId, this.selectedDays, 'day').subscribe({
      next: (response) => {
        if (response.success) {
          this.trends = response.trends || [];
          if (this.viewInitialized) this.renderTrendChart();
        }
      },
      error: (err) => console.error('Failed to load trends', err)
    });

    this.insightsService.getTopicAnalysis(orgId, this.selectedDays).subscribe({
      next: (response) => {
        if (response.success) this.topics = response.topics || [];
        this.isLoading = false;
        this.dataLoaded = true;
      },
      error: (err) => { console.error('Failed to load topics', err); this.isLoading = false; }
    });

    this.insightsService.getNeedsAttention(orgId, this.selectedDays).subscribe({
      next: (response: { success: boolean; items: NeedsAttentionItem[] }) => {
        if (response.success) this.needsAttention = response.items || [];
      },
      error: (err: any) => console.error('Failed to load needs attention', err)
    });
  }

  private renderCharts(): void {
    this.renderTrendChart();
    this.renderGauge();
  }

  private renderTrendChart(): void {
    if (!this.trendChartRef?.nativeElement || this.trends.length === 0) return;
    this.trendChartInstance?.destroy();

    const labels = this.trends.map(t => {
      const d = new Date(t.Period);
      return `${d.getMonth() + 1}/${d.getDate()}`;
    });
    const data = this.trends.map(t => t.ConversationCount || 0);

    this.trendChartInstance = new Chart(this.trendChartRef.nativeElement, {
      type: 'line',
      data: {
        labels,
        datasets: [{
          label: 'Conversations',
          data,
          borderColor: '#6366f1',
          backgroundColor: 'rgba(99, 102, 241, 0.08)',
          borderWidth: 2.5,
          pointBackgroundColor: '#6366f1',
          pointRadius: 4,
          pointHoverRadius: 6,
          fill: true,
          tension: 0.4
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: {
            backgroundColor: '#0f172a',
            titleColor: '#94a3b8',
            bodyColor: 'white',
            padding: 12,
            cornerRadius: 8,
            callbacks: {
              label: (ctx) => ` ${ctx.parsed.y} conversations`
            }
          }
        },
        scales: {
          x: {
            grid: { display: false },
            ticks: { color: '#94a3b8', font: { size: 11 } }
          },
          y: {
            beginAtZero: true,
            grid: { color: '#f1f5f9' },
            ticks: { color: '#94a3b8', font: { size: 11 }, stepSize: 1, precision: 0 }
          }
        }
      }
    });
  }

  private renderGauge(): void {
    if (!this.gaugeChartRef?.nativeElement || !this.summary) return;
    this.gaugeChartInstance?.destroy();

    const score = Math.round(this.summary.AverageHealthScore || 0);
    const remaining = 100 - score;
    const color = score >= 70 ? '#22c55e' : score >= 40 ? '#f59e0b' : '#ef4444';

    this.gaugeChartInstance = new Chart(this.gaugeChartRef.nativeElement, {
      type: 'doughnut',
      data: {
        datasets: [{
          data: [score, remaining],
          backgroundColor: [color, '#f1f5f9'],
          borderWidth: 0,
          circumference: 270,
          rotation: 225
        } as any]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        cutout: '78%',
        plugins: {
          legend: { display: false },
          tooltip: { enabled: false }
        }
      }
    });
  }

  onDaysChange(days: number): void {
    this.selectedDays = days;
    this.loadDashboardData();
  }

  getHealthScoreClass(score: number): string {
    if (score >= 70) return 'health-good';
    if (score >= 40) return 'health-medium';
    return 'health-poor';
  }

  getHealthScoreColor(score: number): string {
    if (score >= 70) return '#22c55e';
    if (score >= 40) return '#f59e0b';
    return '#ef4444';
  }

  getSourceTypes(): { key: string; value: number }[] {
    const sourceData = this.summary?.BySourceType || (this.summary as any)?.bySourceType;
    if (!sourceData) return [];
    return Object.entries(sourceData).map(([key, value]) => ({ key, value: value as number }));
  }

  getSourcePercentage(count: number): number {
    if (!this.summary) return 0;
    return (count / (this.summary.TotalConversations || 1)) * 100;
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
    return topic.HighSeverityCount > 0 ? 'high' : 'low';
  }

  getRiskClass(risk: string): string {
    switch (risk?.toLowerCase()) {
      case 'high': return 'risk-high';
      case 'medium': return 'risk-medium';
      case 'low': return 'risk-low';
      default: return '';
    }
  }
}

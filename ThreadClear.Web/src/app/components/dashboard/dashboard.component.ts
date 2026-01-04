import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
import { OrganizationService, Organization } from '../../services/organization.service';
import { InsightsService, InsightSummary, InsightTrend, TopicBreakdown } from '../../services/insights.service';

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

  selectedDays = 30;
  isLoading = true;
  error: string | null = null;

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
    // Handle both PascalCase and camelCase
    return Math.max(...this.trends.map(t => t.conversationCount || (t as any).conversationCount || 0), 1);
  }

  getSourceTypes(): { key: string; value: number }[] {
    // Handle both PascalCase and camelCase
    const sourceData = this.summary?.BySourceType || (this.summary as any)?.bySourceType;
    if (!sourceData) return [];
    return Object.entries(sourceData).map(([key, value]) => ({ key, value: value as number }));
  }

  getSourcePercentage(count: number): number {
    if (!this.summary) return 0;
    const total = this.summary.TotalConversations || (this.summary as any)?.totalConversations || 1;
    return (count / total) * 100;
  }

  getTopCategories(topic: TopicBreakdown): { key: string; value: number }[] {
    // Handle both PascalCase and camelCase
    const categoryData = topic.byCategory || (topic as any)?.byCategory;
    if (!categoryData) return [];
    return Object.entries(categoryData)
      .map(([key, value]) => ({ key, value: value as number }))
      .slice(0, 3);
  }

  getTopicSeverity(topic: TopicBreakdown): string {
    if (topic.highSeverityCount > 0) return 'high';
    return 'low';
  }
}

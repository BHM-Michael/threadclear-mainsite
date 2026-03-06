import { Component, Input, OnChanges, SimpleChanges, Output, EventEmitter, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { InsightTrend } from '../../services/insights.service';

interface ChartPoint {
  x: number;
  y: number;
  value: number;
  label: string;
}

@Component({
  selector: 'app-trend-chart',
  templateUrl: './trend-chart.component.html',
  styleUrls: ['./trend-chart.component.scss'],
  encapsulation: ViewEncapsulation.None
})
export class TrendChartComponent implements OnChanges {
  @Input() trends: InsightTrend[] = [];
  @Input() days: number = 30;
  @Input() panelConversations: any[] = [];
  @Input() panelDate: Date | string | null = null;

  @Output() dateClicked = new EventEmitter<string>();

  // SVG dimensions
  svgWidth = 600;
  svgHeight = 220;
  paddingLeft = 45;
  paddingRight = 20;
  paddingTop = 15;
  paddingBottom = 40;

  panelOpen = false;

  private hideTimer: any;

  healthPoints: ChartPoint[] = [];
  riskPoints: ChartPoint[] = [];
  healthLinePath = '';
  riskLinePath = '';
  healthAreaPath = '';
  xLabels: { x: number; label: string }[] = [];
  yLabels: { y: number; label: string }[] = [];
  gridLines: number[] = [];

  tooltip: { visible: boolean; x: number; y: number; trend: InsightTrend | null } = {
    visible: false, x: 0, y: 0, trend: null
  };

  get chartWidth(): number {
    return this.svgWidth - this.paddingLeft - this.paddingRight;
  }

  get chartHeight(): number {
    return this.svgHeight - this.paddingTop - this.paddingBottom;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['trends'] && this.trends?.length > 0) {
      this.buildChart();
    }
  }

  private buildChart(): void {
    const data = this.trends;
    if (!data.length) return;

    // Y scale: health score 0-100
    const yMin = 0;
    const yMax = 100;

    // X scale
    const xStep = this.chartWidth / Math.max(data.length - 1, 1);

    // Build health score points
    this.healthPoints = data.map((t, i) => ({
      x: this.paddingLeft + i * xStep,
      y: this.paddingTop + this.chartHeight - ((t.AverageHealthScore - yMin) / (yMax - yMin)) * this.chartHeight,
      value: Math.round(t.AverageHealthScore),
      label: this.formatPeriod(t.Period)
    }));

    // Build risk count points (scaled to fit chart height, max = max risk count or 10)
    const maxRisk = Math.max(...data.map(t => t.HighRiskCount), 5);
    this.riskPoints = data.map((t, i) => ({
      x: this.paddingLeft + i * xStep,
      y: this.paddingTop + this.chartHeight - (t.HighRiskCount / maxRisk) * this.chartHeight,
      value: t.HighRiskCount,
      label: this.formatPeriod(t.Period)
    }));

    // Build SVG paths
    this.healthLinePath = this.buildLinePath(this.healthPoints);
    this.riskLinePath = this.buildLinePath(this.riskPoints);
    this.healthAreaPath = this.buildAreaPath(this.healthPoints);

    // X axis labels — show max 7 evenly spaced
    const labelStep = Math.ceil(data.length / 7);
    this.xLabels = data
      .map((t, i) => ({ x: this.paddingLeft + i * xStep, label: this.formatPeriod(t.Period), index: i }))
      .filter((_, i) => i % labelStep === 0 || i === data.length - 1);

    // Y axis labels and grid lines
    this.yLabels = [0, 25, 50, 75, 100].map(v => ({
      y: this.paddingTop + this.chartHeight - (v / 100) * this.chartHeight,
      label: v.toString()
    }));
    this.gridLines = [25, 50, 75, 100].map(v =>
      this.paddingTop + this.chartHeight - (v / 100) * this.chartHeight
    );
  }

  private buildLinePath(points: ChartPoint[]): string {
    if (!points.length) return '';
    return points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x} ${p.y}`).join(' ');
  }

  private buildAreaPath(points: ChartPoint[]): string {
    if (!points.length) return '';
    const bottom = this.paddingTop + this.chartHeight;
    const line = points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x} ${p.y}`).join(' ');
    return `${line} L ${points[points.length - 1].x} ${bottom} L ${points[0].x} ${bottom} Z`;
  }

  private formatPeriod(period: string): string {
    const date = new Date(period);
    if (isNaN(date.getTime())) return period;
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  }

  onDotClick(trend: InsightTrend): void {
    const date = new Date(trend.Period).toISOString().split('T')[0];
    this.dateClicked.emit(date);
    this.panelOpen = true;
  }

  closePanel(): void {
    this.panelOpen = false;
  }

  showTooltip(point: ChartPoint, trend: InsightTrend): void {
    clearTimeout(this.hideTimer);
    this.tooltip = { visible: true, x: point.x, y: point.y, trend };
  }

  hideTooltip(): void {
    this.hideTimer = setTimeout(() => {
      this.tooltip = { visible: false, x: 0, y: 0, trend: null };
    }, 200);
  }

  getCoachingNudge(conv: any): string {
    const risk = conv.OverallRisk || conv.overallRisk;
    const unanswered = conv.UnansweredQuestionsCount || conv.unansweredQuestionsCount || 0;
    const tension = conv.TensionPointsCount || conv.tensionPointsCount || 0;

    if (risk === 'High' && tension > 0)
      return 'Follow up — tension detected, may need intervention';
    if (unanswered > 1)
      return `${unanswered} questions still waiting on a response`;
    if (tension > 0)
      return 'Check in — communication friction detected';
    if (risk === 'High')
      return 'Review this conversation — health score is low';
    return 'Worth a quick check-in';
  }

  getHealthClass(score: number): string {
    if (score >= 70) return 'health-good';
    if (score >= 40) return 'health-medium';
    return 'health-poor';
  }

  // Keep original untouched — used by SVG dots
  getHealthColor(score: number): string {
    if (score >= 70) return '#22c55e';
    if (score >= 40) return '#f59e0b';
    return '#ef4444';
  }

  showAllCards = false;

  getPanelCount(risk: string): number {
    return this.panelConversations.filter(c =>
      (c.OverallRisk || c.overallRisk) === risk).length;
  }

  getPanelAvgHealth(): number {
    if (!this.panelConversations.length) return 0;
    const sum = this.panelConversations.reduce((acc, c) =>
      acc + (c.HealthScore || c.healthScore || 0), 0);
    return Math.round(sum / this.panelConversations.length);
  }

  getPanelTotalUnanswered(): number {
    return this.panelConversations.reduce((acc, c) =>
      acc + (c.UnansweredQuestionsCount || c.unansweredQuestionsCount || 0), 0);
  }

  getPanelTotalTension(): number {
    return this.panelConversations.reduce((acc, c) =>
      acc + (c.TensionPointsCount || c.tensionPointsCount || 0), 0);
  }

  getAttentionCards(): any[] {
    return this.panelConversations
      .filter(c => (c.OverallRisk || c.overallRisk) === 'High' ||
        (c.UnansweredQuestionsCount || c.unansweredQuestionsCount) > 0 ||
        (c.TensionPointsCount || c.tensionPointsCount) > 0)
      .sort((a, b) => (a.HealthScore || a.healthScore || 0) - (b.HealthScore || b.healthScore || 0));
  }

  getVisibleCards(): any[] {
    if (this.showAllCards) return this.panelConversations;
    const attention = this.getAttentionCards();
    return attention.length > 0 ? attention.slice(0, 3) : this.panelConversations.slice(0, 3);
  }

  getSourceLabel(sourceType: string): string {
    const labels: Record<string, string> = {
      'email': '📧 Email',
      'Email': '📧 Email',
      'slack': '💬 Slack',
      'Slack': '💬 Slack',
      'teams': '🟦 Teams',
      'sms': '📱 SMS',
      'SMS': '📱 SMS',
      'sms_unstructured': '📱 SMS / Text',
      'conversation': '💬 Conversation',
      'audio': '🎙️ Audio',
      'image': '🖼️ Image'
    };
    return labels[sourceType] || sourceType;
  }
}

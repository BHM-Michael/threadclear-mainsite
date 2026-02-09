import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { ApiService, AnalysisRecord } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-analysis-hub',
  templateUrl: './analysis-hub.component.html',
  styleUrls: ['./analysis-hub.component.scss']
})
export class AnalysisHubComponent implements OnInit, OnDestroy {
  analyses: any[] = [];
  filteredAnalyses: any[] = [];
  loading = true;
  error = '';

  // Filters
  sourceFilter = 'all';
  riskFilter = 'all';
  dateFilter = 'all';

  // Stats
  totalAnalyses = 0;
  avgHealthScore = 0;
  totalFindings = 0;
  highRiskCount = 0;

  private destroy$ = new Subject<void>();

  constructor(
    private apiService: ApiService,
    private authService: AuthService,
    private router: Router
  ) { }

  ngOnInit() {
    if (!this.authService.isLoggedIn) {
      this.router.navigate(['/login']);
      return;
    }
    this.loadAnalyses();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadAnalyses() {
    this.loading = true;
    this.error = '';

    this.apiService.getMyAnalyses(100)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          if (response.success) {
            this.analyses = response.analyses || [];
            this.calculateStats();
            this.applyFilters();
          } else {
            this.error = 'Failed to load analysis history';
          }
          this.loading = false;
        },
        error: (err) => {
          console.error('Error loading analyses:', err);
          this.error = 'Failed to load analysis history';
          this.loading = false;
        }
      });
  }

  calculateStats() {
    this.totalAnalyses = this.analyses.length;

    if (this.totalAnalyses > 0) {
      const totalHealth = this.analyses.reduce((sum, a) => sum + this.getHealthScore(a), 0);
      this.avgHealthScore = Math.round(totalHealth / this.totalAnalyses);
    } else {
      this.avgHealthScore = 0;
    }

    this.totalFindings = this.analyses.reduce((sum, a) => sum + this.getTotalFindingCount(a), 0);
    this.highRiskCount = this.analyses.filter(a => this.getRiskLevel(a) === 'High').length;
  }

  applyFilters() {
    this.filteredAnalyses = this.analyses.filter(a => {
      const source = this.getSource(a);
      const riskLevel = this.getRiskLevel(a);
      const createdAt = this.getCreatedAt(a);

      // Source filter
      if (this.sourceFilter !== 'all' && source !== this.sourceFilter) {
        return false;
      }

      // Risk filter
      if (this.riskFilter !== 'all' && riskLevel !== this.riskFilter) {
        return false;
      }

      // Date filter
      if (this.dateFilter !== 'all' && createdAt) {
        const analysisDate = new Date(createdAt);
        const now = new Date();

        if (this.dateFilter === 'today') {
          const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
          if (analysisDate < today) return false;
        } else if (this.dateFilter === 'week') {
          const weekAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
          if (analysisDate < weekAgo) return false;
        } else if (this.dateFilter === 'month') {
          const monthAgo = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
          if (analysisDate < monthAgo) return false;
        }
      }

      return true;
    });
  }

  onFilterChange() {
    this.applyFilters();
  }

  // Helper methods to handle PascalCase/camelCase
  getHealthScore(a: any): number {
    return a.healthScore ?? a.HealthScore ?? 0;
  }

  getRiskLevel(a: any): string {
    return a.riskLevel || a.RiskLevel || 'Low';
  }

  getSource(a: any): string {
    return a.source || a.Source || '';
  }

  getCreatedAt(a: any): string {
    return a.createdAt || a.CreatedAt || '';
  }

  getParticipantCount(a: any): number {
    return a.participantCount ?? a.ParticipantCount ?? 0;
  }

  getFindings(a: any): any[] {
    return a.findings || a.Findings || [];
  }

  getUniqueSources(): string[] {
    const sources = new Set(this.analyses.map(a => this.getSource(a)));
    return Array.from(sources).filter(s => s !== '');
  }

  getFindingCount(analysis: any, type: string): number {
    const findings = this.getFindings(analysis);
    return findings.filter((f: any) => (f.findingType || f.FindingType) === type).length;
  }

  getTotalFindingCount(analysis: any): number {
    return this.getFindings(analysis).length;
  }

  getHealthClass(score: number): string {
    if (score >= 70) return 'health-good';
    if (score >= 40) return 'health-medium';
    return 'health-poor';
  }

  getRiskClass(risk: string): string {
    switch (risk?.toLowerCase()) {
      case 'high': return 'risk-high';
      case 'medium': return 'risk-medium';
      default: return 'risk-low';
    }
  }

  getSourceIcon(source: string): string {
    switch (source?.toLowerCase()) {
      case 'gmail': return '📧';
      case 'slack': return '💬';
      case 'teams': return '👥';
      case 'outlook': return '📬';
      case 'chrome': return '🌐';
      case 'web': return '📝';
      default: return '📝';
    }
  }

  formatDate(dateString: string): string {
    if (!dateString) return 'Unknown';

    const date = new Date(dateString);
    if (isNaN(date.getTime())) return 'Invalid Date';

    const now = new Date();
    const diff = now.getTime() - date.getTime();

    // Less than 24 hours
    if (diff < 24 * 60 * 60 * 1000) {
      const hours = Math.floor(diff / (60 * 60 * 1000));
      if (hours < 1) {
        const minutes = Math.floor(diff / (60 * 1000));
        return minutes <= 1 ? 'Just now' : `${minutes} minutes ago`;
      }
      return hours === 1 ? '1 hour ago' : `${hours} hours ago`;
    }

    // Less than 7 days
    if (diff < 7 * 24 * 60 * 60 * 1000) {
      const days = Math.floor(diff / (24 * 60 * 60 * 1000));
      return days === 1 ? 'Yesterday' : `${days} days ago`;
    }

    // Otherwise show date
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }

  goToAnalyzer() {
    this.router.navigate(['/analyze']);
  }
}
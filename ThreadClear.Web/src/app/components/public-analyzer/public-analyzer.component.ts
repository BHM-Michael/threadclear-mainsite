import { Component, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { ApiService } from '../../services/api.service';

@Component({
    selector: 'app-public-analyzer',
    templateUrl: './public-analyzer.component.html',
    styleUrls: ['./public-analyzer.component.scss']
})
export class PublicAnalyzerComponent implements OnDestroy {
    conversationText = '';
    sourceType = 'simple';
    loading = false;
    results: any = null;
    error = '';
    remainingScans: number | null = null;
    rateLimited = false;
    showSignupCta = false;

    // Progressive loading states
    sectionsLoading: { [key: string]: boolean } = {
        summary: false, questions: false, tensions: false,
        health: false, actions: false, misalignments: false
    };
    sectionsComplete: { [key: string]: boolean } = {
        summary: false, questions: false, tensions: false,
        health: false, actions: false, misalignments: false
    };
    sectionsError: { [key: string]: string } = {};
    progressPercent = 0;

    private destroy$ = new Subject<void>();

    sampleConversation = `From: Sarah Chen
To: Mike Johnson
Subject: Q3 Budget Review

Mike,

I've reviewed the Q3 numbers and I'm concerned about the marketing spend. We're 40% over budget with two months left. Can we schedule a call this week to discuss?

Also, did you ever hear back from the vendor about the revised pricing?

Thanks,
Sarah

---

From: Mike Johnson
To: Sarah Chen

Sarah,

I think the marketing spend is justified given the campaign results. We're seeing 3x ROI on the digital ads.

Let's talk Thursday. I'm free after 2pm.

Mike`;

    constructor(
        private apiService: ApiService,
        private router: Router
    ) { }

    ngOnInit(): void {
        // Silently wake the database before user does anything
        this.apiService.warmup().subscribe();
    }

    ngOnDestroy() {
        this.destroy$.next();
        this.destroy$.complete();
    }

    analyze() {
        if (!this.conversationText.trim()) {
            this.error = 'Please paste a conversation to analyze.';
            return;
        }

        this.loading = true;
        this.error = '';
        this.results = null;
        this.rateLimited = false;
        this.progressPercent = 0;

        // Reset section states
        Object.keys(this.sectionsLoading).forEach(key => {
            this.sectionsLoading[key] = false;
            this.sectionsComplete[key] = false;
            this.sectionsError[key] = '';
        });

        this.sourceType = this.apiService.detectSourceType(this.conversationText);

        // Step 1: Check rate limit
        this.apiService.startPublicScan(this.conversationText.length, this.sourceType)
            .pipe(takeUntil(this.destroy$))
            .subscribe({
                next: (gate) => {
                    if (!gate.allowed) {
                        this.loading = false;
                        this.rateLimited = true;
                        this.remainingScans = gate.remainingScans;
                        return;
                    }

                    this.remainingScans = gate.remainingScans;

                    // Step 2: Quick parse (regex, instant)
                    this.apiService.quickParsePublic(this.conversationText)
                        .pipe(takeUntil(this.destroy$))
                        .subscribe({
                            next: (parseResult) => {
                                // Show participants + messages immediately
                                this.results = {
                                    CapsuleId: 'tc-' + Date.now(),
                                    SourceType: parseResult.sourceType || 'conversation',
                                    Participants: parseResult.participants || [],
                                    Messages: parseResult.messages || [],
                                    Metadata: parseResult.metadata || {},
                                    Summary: null,
                                    Analysis: {
                                        UnansweredQuestions: null,
                                        TensionPoints: null,
                                        Misalignments: null,
                                        ConversationHealth: null
                                    },
                                    SuggestedActions: null
                                };

                                this.progressPercent = 15;
                                this.loading = false; // Hide main spinner, show results with section spinners

                                // Step 3: Fire all AI sections in parallel
                                this.loadSection('summary');
                                this.loadSection('questions');
                                this.loadSection('tensions');
                                this.loadSection('health');
                                this.loadSection('actions');
                                this.loadSection('misalignments');
                            },
                            error: (err) => {
                                console.error('QuickParse failed:', err);
                                this.error = 'Error parsing conversation. Please try again.';
                                this.loading = false;
                            }
                        });
                },
                error: (err) => {
                    this.loading = false;
                    if (err.status === 429) {
                        this.rateLimited = true;
                        this.remainingScans = 0;
                    } else {
                        this.error = 'Something went wrong. Please try again.';
                    }
                }
            });
    }

    private loadSection(section: string) {
        this.sectionsLoading[section] = true;

        this.apiService.analyzeSectionPublic(this.conversationText, section)
            .pipe(takeUntil(this.destroy$))
            .subscribe({
                next: (response) => {
                    if (response.success) {
                        this.updateResultsSection(section, response.data);
                    } else {
                        this.sectionsError[section] = response.error || 'Failed';
                    }
                    this.sectionsLoading[section] = false;
                    this.sectionsComplete[section] = true;
                    this.updateProgress();
                },
                error: (err) => {
                    console.error(`Section ${section} error:`, err);
                    this.sectionsError[section] = 'Failed to analyze';
                    this.sectionsLoading[section] = false;
                    this.sectionsComplete[section] = true;
                    this.updateProgress();
                }
            });
    }

    private updateResultsSection(section: string, data: any) {
        if (!this.results) return;

        switch (section) {
            case 'summary':
                this.results = { ...this.results, Summary: data?.Summary || data?.summary || null };
                break;
            case 'questions':
                this.results = {
                    ...this.results,
                    Analysis: { ...this.results.Analysis, UnansweredQuestions: data?.UnansweredQuestions || data?.unansweredQuestions || [] }
                };
                break;
            case 'tensions':
                this.results = {
                    ...this.results,
                    Analysis: { ...this.results.Analysis, TensionPoints: data?.TensionPoints || data?.tensionPoints || [] }
                };
                break;
            case 'health':
                this.results = {
                    ...this.results,
                    Analysis: { ...this.results.Analysis, ConversationHealth: data?.ConversationHealth || data?.conversationHealth || data }
                };
                break;
            case 'actions':
                this.results = { ...this.results, SuggestedActions: data?.SuggestedActions || data?.suggestedActions || [] };
                break;
            case 'misalignments':
                this.results = {
                    ...this.results,
                    Analysis: { ...this.results.Analysis, Misalignments: data?.Misalignments || data?.misalignments || [] }
                };
                break;
        }
    }

    private updateProgress() {
        const total = Object.keys(this.sectionsComplete).length;
        const complete = Object.values(this.sectionsComplete).filter(v => v).length;
        this.progressPercent = 15 + Math.round((complete / total) * 85);

        if (complete === total) {
            setTimeout(() => this.showSignupCta = true, 800); // slight delay feels natural
        }
    }

    get allSectionsComplete(): boolean {
        return Object.values(this.sectionsComplete).every(v => v);
    }

    loadSample() {
        this.conversationText = this.sampleConversation;
    }

    clear() {
        this.conversationText = '';
        this.results = null;
        this.error = '';
        this.rateLimited = false;
        this.progressPercent = 0;
        Object.keys(this.sectionsLoading).forEach(key => {
            this.sectionsLoading[key] = false;
            this.sectionsComplete[key] = false;
            this.sectionsError[key] = '';
        });
    }

    goToSignup() { this.router.navigate(['/register']); }
    goToLogin() { this.router.navigate(['/login']); }
}
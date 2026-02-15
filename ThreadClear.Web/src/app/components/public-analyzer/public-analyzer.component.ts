import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService, PublicAnalysisResponse } from '../../services/api.service';

@Component({
    selector: 'app-public-analyzer',
    templateUrl: './public-analyzer.component.html',
    styleUrls: ['./public-analyzer.component.scss']
})
export class PublicAnalyzerComponent {
    conversationText = '';
    sourceType = 'simple';
    loading = false;
    results: any = null;
    error = '';
    remainingScans: number | null = null;
    rateLimited = false;

    // Sample conversations for the placeholder
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

    analyze() {
        if (!this.conversationText.trim()) {
            this.error = 'Please paste a conversation to analyze.';
            return;
        }

        this.loading = true;
        this.error = '';
        this.results = null;
        this.rateLimited = false;

        // Auto-detect source type
        this.sourceType = this.apiService.detectSourceType(this.conversationText);

        this.apiService.analyzePublic({
            conversationText: this.conversationText,
            sourceType: this.sourceType
        }).subscribe({
            next: (response: PublicAnalysisResponse) => {
                this.loading = false;
                if (response.success) {
                    this.results = response.capsule;
                    this.remainingScans = response.remainingScans;
                } else {
                    this.error = response.error || 'Analysis failed. Please try again.';
                }
            },
            error: (err) => {
                this.loading = false;
                if (err.status === 429) {
                    this.rateLimited = true;
                    this.remainingScans = 0;
                    this.error = '';
                } else {
                    this.error = err.error?.error || 'Something went wrong. Please try again.';
                }
            }
        });
    }

    loadSample() {
        this.conversationText = this.sampleConversation;
    }

    clear() {
        this.conversationText = '';
        this.results = null;
        this.error = '';
        this.rateLimited = false;
    }

    goToSignup() {
        this.router.navigate(['/register']);
    }

    goToLogin() {
        this.router.navigate(['/login']);
    }
}
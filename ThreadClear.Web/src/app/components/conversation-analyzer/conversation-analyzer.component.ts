import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { SpellCheckIssue, MessageSpellCheckResult } from '../../services/api.service';

@Component({
  selector: 'app-conversation-analyzer',
  templateUrl: './conversation-analyzer.component.html',
  styleUrls: ['./conversation-analyzer.component.scss']
})
export class ConversationAnalyzerComponent implements OnInit, OnDestroy {
  conversationText = '';
  draftMessage = '';
  sourceType = 'simple';
  parsingMode = 2;
  loading = false;
  results: any = null;
  draftAnalysis: any = null;
  error = '';
  loadingMessage = 'Analyzing conversation...';

  // Image upload - supports multiple
  inputMode: 'text' | 'image' | 'audio' = 'text';
  selectedImages: File[] = [];
  selectedAudio: File | null = null;
  imagePreviews: string[] = [];

  // Streaming/Progressive loading
  useStreaming = true;
  isStreaming = false;
  streamingProgress = 0;
  streamStatus = '';
  streamingText = '';

  spellCheckEnabled = false;
  spellCheckLoading = false;
  spellCheckResults: Map<string, SpellCheckIssue[]> = new Map();
  totalSpellIssues = 0;

  // Section loading states - for progressive UI updates
  sectionsLoading: { [key: string]: boolean } = {
    summary: false,
    questions: false,
    tensions: false,
    health: false,
    actions: false,
    misalignments: false
  };

  sectionsComplete: { [key: string]: boolean } = {
    summary: false,
    questions: false,
    tensions: false,
    health: false,
    actions: false,
    misalignments: false
  };

  sectionsError: { [key: string]: string } = {};

  private destroy$ = new Subject<void>();

  constructor(
    private apiService: ApiService,
    private authService: AuthService,
    private router: Router
  ) { }

  ngOnInit() {
    console.log("Current user:", this.authService.currentUser);
    console.log("Is logged in:", this.authService.isLoggedIn);
    console.log("Is admin:", this.authService.isAdmin);

    if (!this.authService.isLoggedIn) {
      this.router.navigate(['/login']);
    }

    document.addEventListener('paste', this.onPaste.bind(this));
  }

  ngOnDestroy() {
    document.removeEventListener('paste', this.onPaste.bind(this));
    this.destroy$.next();
    this.destroy$.complete();
  }

  onPaste(event: ClipboardEvent) {
    const items = event.clipboardData?.items;
    if (!items) return;

    for (let i = 0; i < items.length; i++) {
      if (items[i].type.startsWith('image/')) {
        event.preventDefault();
        const file = items[i].getAsFile();
        if (file) {
          this.inputMode = 'image';
          this.addImages([file]);
          console.log('Image pasted from clipboard');
        }
        break;
      }
    }
  }

  startProgressMessages() {
    const messages = [
      { text: 'Parsing conversation...', delay: 0 },
      { text: 'Identifying participants...', delay: 1500 },
      { text: 'Detecting unanswered questions...', delay: 3000 },
      { text: 'Analyzing tension points...', delay: 5000 },
      { text: 'Assessing conversation health...', delay: 7000 },
      { text: 'Generating insights...', delay: 9000 },
      { text: 'Analyzing your draft...', delay: 11000 },
      { text: 'Finalizing results...', delay: 13000 }
    ];

    messages.forEach(msg => {
      setTimeout(() => {
        if (this.loading) {
          this.loadingMessage = msg.text;
        }
      }, msg.delay);
    });
  }

  get currentUser() {
    return this.authService.currentUser;
  }

  get isAdmin() {
    return this.authService.isAdmin;
  }

  get permissions(): any {
    const user = this.currentUser as any;
    return user?.permissions || user?.Permissions;
  }

  get userEmail(): string {
    const user = this.currentUser as any;
    return user?.Email || user?.email || '';
  }

  onAudioSelected(event: any) {
    const file = event.target.files[0];
    if (file) {
      this.selectAudio(file);
    }
    event.target.value = '';
  }

  onDropAudio(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    const files = event.dataTransfer?.files;
    if (files && files.length > 0) {
      this.selectAudio(files[0]);
    }
  }

  selectAudio(file: File) {
    if (file.size > 25 * 1024 * 1024) {
      this.error = 'Audio file must be less than 25MB';
      return;
    }
    this.selectedAudio = file;
    this.error = '';
  }

  removeAudio(event: Event) {
    event.stopPropagation();
    this.selectedAudio = null;
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }

  onFileSelected(event: any) {
    const files = event.target.files;
    if (files) {
      this.addImages(Array.from(files));
    }
    event.target.value = '';
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    const files = event.dataTransfer?.files;
    if (files && files.length > 0) {
      this.addImages(Array.from(files));
    }
  }

  addImages(files: File[]) {
    for (const file of files) {
      if (!file.type.startsWith('image/')) {
        this.error = 'Please select only image files';
        continue;
      }
      if (file.size > 10 * 1024 * 1024) {
        this.error = 'Each image must be less than 10MB';
        continue;
      }
      if (this.selectedImages.length >= 10) {
        this.error = 'Maximum 10 images allowed';
        break;
      }

      this.selectedImages.push(file);
      this.error = '';

      const reader = new FileReader();
      reader.onload = (e) => {
        this.imagePreviews.push(e.target?.result as string);
      };
      reader.readAsDataURL(file);
    }
  }

  removeImage(index: number) {
    this.selectedImages.splice(index, 1);
    this.imagePreviews.splice(index, 1);
  }

  analyze() {
    if (this.inputMode === 'image' && this.selectedImages.length > 0) {
      this.analyzeImages();
    } else if (this.inputMode === 'audio' && this.selectedAudio) {
      this.analyzeAudio();
    } else if (this.useStreaming && this.inputMode === 'text') {
      this.analyzeTextProgressive();
    } else {
      this.analyzeText();
    }
  }

  // NEW: Progressive analysis - shows results as they come in
  analyzeTextProgressive() {
    this.loading = true;
    this.isStreaming = true;
    this.streamingProgress = 0;
    this.streamStatus = 'Parsing conversation...';
    this.error = '';
    this.results = null;
    this.draftAnalysis = null;
    this.insightStored = false;

    // Reset section states
    Object.keys(this.sectionsLoading).forEach(key => {
      this.sectionsLoading[key] = false;
      this.sectionsComplete[key] = false;
      this.sectionsError[key] = '';
    });

    // Phase 1: Quick parse (instant, no AI)
    this.apiService.quickParse(this.conversationText)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (parseResult) => {
          console.log('QuickParse result:', parseResult);

          // Initialize results with parsed data - user sees this immediately
          this.results = {
            CapsuleId: 'tc-' + Date.now(),
            SourceType: parseResult.sourceType || 'conversation',
            Participants: parseResult.participants || [],
            Messages: parseResult.messages || [],
            Metadata: parseResult.metadata || {},
            Summary: null,  // Will be filled by AI
            Analysis: {
              UnansweredQuestions: null,  // null = loading, [] = empty
              TensionPoints: null,
              Misalignments: null,
              ConversationHealth: null
            },
            SuggestedActions: null
          };

          this.streamingProgress = 20;
          this.streamStatus = 'Running AI analysis...';

          // Hide the loading overlay - show results with spinners
          this.isStreaming = false;
          this.loading = false;

          // Phase 2: Fire all AI analysis sections in parallel
          this.loadSectionParallel('summary');
          this.loadSectionParallel('questions');
          this.loadSectionParallel('tensions');
          this.loadSectionParallel('health');
          this.loadSectionParallel('actions');
          this.loadSectionParallel('misalignments');

          // Also analyze draft if provided
          if (this.draftMessage) {
            this.analyzeDraftMessage();
          }
        },
        error: (err) => {
          console.error('QuickParse error, falling back to full analysis:', err);
          // Fallback to the original full analysis
          this.analyzeText();
        }
      });
  }

  private loadSectionParallel(section: string, text?: string) {
    const conversationText = text || this.conversationText;

    if (!this.shouldLoadSection(section)) {
      this.sectionsComplete[section] = true;
      this.updateProgress();
      return;
    }

    this.sectionsLoading[section] = true;

    this.apiService.analyzeSection(conversationText, section)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          console.log(`Section ${section} response:`, response);
          if (response.success) {
            this.updateResultsSection(section, response.data);
          } else {
            this.sectionsError[section] = response.error || 'Analysis failed';
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

  private shouldLoadSection(section: string): boolean {
    if (this.isAdmin) return true;
    if (!this.permissions) return true;

    const perms = this.permissions;
    switch (section) {
      case 'questions':
        return perms.UnansweredQuestions || perms.unansweredQuestions || false;
      case 'tensions':
        return perms.TensionPoints || perms.tensionPoints || false;
      case 'misalignments':
        return perms.Misalignments || perms.misalignments || false;
      case 'health':
        return perms.ConversationHealth || perms.conversationHealth || false;
      case 'actions':
        return perms.SuggestedActions || perms.suggestedActions || false;
      case 'summary':
        return true; // Always show summary
      default:
        return true;
    }
  }

  private updateResultsSection(section: string, data: any) {
    if (!this.results) return;

    console.log(`Updating section ${section} with data:`, JSON.stringify(data));

    switch (section) {
      case 'summary':
        const summary = data?.Summary || data?.summary || (typeof data === 'string' ? data : null);
        console.log('Extracted summary:', summary);
        // Force new object reference for change detection
        this.results = { ...this.results, Summary: summary };
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
    this.streamingProgress = 20 + Math.round((complete / total) * 80);

    // Check if ALL sections are complete
    const allComplete = complete === total;

    if (allComplete && this.results) {
      this.streamStatus = 'Analysis complete!';

      // Store insight now that we have the full capsule
      this.storeInsightIfNeeded();
    } else {
      // Update status message based on what's still loading
      const stillLoading = Object.entries(this.sectionsLoading)
        .filter(([_, loading]) => loading)
        .map(([section, _]) => section);

      const sectionNames: { [key: string]: string } = {
        summary: 'summary',
        questions: 'unanswered questions',
        tensions: 'tension points',
        health: 'conversation health',
        actions: 'suggested actions',
        misalignments: 'misalignments'
      };
      this.streamStatus = `Analyzing ${sectionNames[stillLoading[0]] || stillLoading[0]}...`;
    }
  }

  private insightStored = false;  // Add this flag to the class properties

  private storeInsightIfNeeded() {
    if (this.insightStored || !this.results) return;
    this.insightStored = true;

    this.apiService.storeInsight(this.results)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          console.log('Insight storage result:', response);
        },
        error: (err) => {
          console.warn('Failed to store insight:', err);
          // Non-blocking - don't show error to user
        }
      });
  }

  private analyzeDraftMessage() {
    // For draft analysis, use the full endpoint with draft included
    const request: any = {
      conversationText: this.conversationText,
      sourceType: this.sourceType,
      parsingMode: this.parsingMode,
      draftMessage: this.draftMessage
    };

    this.apiService.analyzeConversation(request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.draftAnalysis = this.normalizeDraftAnalysis(response.draftAnalysis);
        },
        error: (err) => {
          console.error('Draft analysis error:', err);
        }
      });
  }

  // Original full analysis method (fallback)
  analyzeText() {
    this.loading = true;
    this.loadingMessage = 'Parsing conversation...';
    this.startProgressMessages();
    this.error = '';
    this.results = null;
    this.draftAnalysis = null;

    const perms = this.permissions;
    const isAdmin = this.isAdmin;

    const request: any = {
      conversationText: this.conversationText,
      sourceType: this.sourceType,
      parsingMode: this.parsingMode,
      draftMessage: this.draftMessage || null
    };

    if (!isAdmin && perms) {
      request.enableUnansweredQuestions = perms.UnansweredQuestions || perms.unansweredQuestions || false;
      request.enableTensionPoints = perms.TensionPoints || perms.tensionPoints || false;
      request.enableMisalignments = perms.Misalignments || perms.misalignments || false;
      request.enableConversationHealth = perms.ConversationHealth || perms.conversationHealth || false;
      request.enableSuggestedActions = perms.SuggestedActions || perms.suggestedActions || false;
    }

    this.apiService.analyzeConversation(request).subscribe({
      next: (response) => {
        this.results = this.filterResultsByPermissions(response.capsule);
        this.draftAnalysis = this.normalizeDraftAnalysis(response.draftAnalysis);
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Error analyzing conversation. Please try again.';
        this.loading = false;
        console.error(err);
      }
    });
  }

  analyzeImages() {
    this.loading = true;
    this.loadingMessage = 'Extracting text from images...';
    this.error = '';
    this.results = null;
    this.draftAnalysis = null;

    // Reset section states
    Object.keys(this.sectionsLoading).forEach(key => {
      this.sectionsLoading[key] = false;
      this.sectionsComplete[key] = false;
      this.sectionsError[key] = '';
    });

    // Step 1: Extract text from images
    this.apiService.extractTextFromImages(this.selectedImages)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (extractResponse) => {
          if (!extractResponse.success || !extractResponse.text) {
            this.error = extractResponse.error || 'Could not extract text from images';
            this.loading = false;
            return;
          }

          const extractedText = extractResponse.text;
          this.loadingMessage = 'Parsing conversation...';

          // Step 2: Quick parse (same as text flow)
          this.apiService.quickParse(extractedText)
            .pipe(takeUntil(this.destroy$))
            .subscribe({
              next: (parseResult) => {
                // Show results immediately
                this.results = {
                  CapsuleId: 'tc-' + Date.now(),
                  SourceType: parseResult.sourceType || 'image',
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

                // Hide loading overlay - show results with section spinners
                this.loading = false;

                // Step 3: Fire parallel AI analysis
                this.loadSectionParallel('summary', extractedText);
                this.loadSectionParallel('questions', extractedText);
                this.loadSectionParallel('tensions', extractedText);
                this.loadSectionParallel('health', extractedText);
                this.loadSectionParallel('actions', extractedText);
                this.loadSectionParallel('misalignments', extractedText);
              },
              error: (err) => {
                console.error('QuickParse error:', err);
                this.error = 'Error parsing extracted text';
                this.loading = false;
              }
            });
        },
        error: (err) => {
          console.error('Image extraction error:', err);
          this.error = 'Error extracting text from images. Please try again.';
          this.loading = false;
        }
      });
  }

  analyzeAudio() {
    if (!this.selectedAudio) return;

    this.loading = true;
    this.loadingMessage = 'Transcribing audio...';
    this.error = '';
    this.results = null;
    this.draftAnalysis = null;

    // Reset section states
    Object.keys(this.sectionsLoading).forEach(key => {
      this.sectionsLoading[key] = false;
      this.sectionsComplete[key] = false;
      this.sectionsError[key] = '';
    });

    const perms = this.permissions;
    const isAdmin = this.isAdmin;

    let enableFlags: any = {};
    if (!isAdmin && perms) {
      enableFlags = {
        enableUnansweredQuestions: perms.UnansweredQuestions || perms.unansweredQuestions || false,
        enableTensionPoints: perms.TensionPoints || perms.tensionPoints || false,
        enableMisalignments: perms.Misalignments || perms.misalignments || false,
        enableConversationHealth: perms.ConversationHealth || perms.conversationHealth || false,
        enableSuggestedActions: perms.SuggestedActions || perms.suggestedActions || false
      };
    }

    this.apiService.analyzeAudio(this.selectedAudio, this.sourceType, this.parsingMode, enableFlags)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.results = this.filterResultsByPermissions(response.capsule);

          // Mark all sections complete
          Object.keys(this.sectionsComplete).forEach(key => {
            this.sectionsComplete[key] = true;
          });

          this.draftAnalysis = this.normalizeDraftAnalysis(response.draftAnalysis);
          this.loading = false;
        },
        error: (err) => {
          this.error = 'Error analyzing audio. Please try again.';
          this.loading = false;
          console.error(err);
        }
      });
  }

  getEnabledFeatures() {
    if (this.isAdmin || !this.permissions) {
      return {};
    }
    return {
      enableUnansweredQuestions: this.permissions.unansweredQuestions,
      enableTensionPoints: this.permissions.tensionPoints,
      enableMisalignments: this.permissions.misalignments,
      enableConversationHealth: this.permissions.conversationHealth,
      enableSuggestedActions: this.permissions.suggestedActions
    };
  }

  filterResultsByPermissions(capsule: any) {
    if (!capsule) return capsule;

    console.log("Filtering results. isAdmin:", this.isAdmin, "permissions:", this.permissions);

    if (this.isAdmin || !this.permissions) {
      return capsule;
    }

    const perms = this.permissions;

    const hasUnanswered = perms.UnansweredQuestions || perms.unansweredQuestions || false;
    const hasTension = perms.TensionPoints || perms.tensionPoints || false;
    const hasMisalignments = perms.Misalignments || perms.misalignments || false;
    const hasHealth = perms.ConversationHealth || perms.conversationHealth || false;
    const hasSuggested = perms.SuggestedActions || perms.suggestedActions || false;

    const analysis = capsule.analysis || capsule.Analysis;

    if (analysis) {
      if (!hasUnanswered) {
        analysis.unansweredQuestions = [];
        analysis.UnansweredQuestions = [];
      }
      if (!hasTension) {
        analysis.tensionPoints = [];
        analysis.TensionPoints = [];
      }
      if (!hasMisalignments) {
        analysis.misalignments = [];
        analysis.Misalignments = [];
      }
      if (!hasHealth) {
        analysis.conversationHealth = null;
        analysis.ConversationHealth = null;
      }
    }
    if (!hasSuggested) {
      capsule.suggestedActions = [];
      capsule.SuggestedActions = [];
    }

    return capsule;
  }

  normalizeDraftAnalysis(draft: any): any {
    if (!draft) return null;

    const toneRaw = draft.Tone || draft.tone;
    const tone = toneRaw ? {
      tone: toneRaw.Tone || toneRaw.tone || '',
      matchesConversationTone: toneRaw.MatchesConversationTone ?? toneRaw.matchesConversationTone ?? true,
      escalationRisk: toneRaw.EscalationRisk || toneRaw.escalationRisk || 'none',
      explanation: toneRaw.Explanation || toneRaw.explanation || ''
    } : null;

    const questionsCoveredRaw = draft.QuestionsCovered || draft.questionsCovered || [];
    const questionsCovered = questionsCoveredRaw.map((q: any) => ({
      question: q.Question || q.question || '',
      addressed: q.Addressed ?? q.addressed ?? false,
      howAddressed: q.HowAddressed || q.howAddressed || null
    }));

    const riskFlagsRaw = draft.RiskFlags || draft.riskFlags || [];
    const riskFlags = riskFlagsRaw.map((r: any) => ({
      type: r.Type || r.type || '',
      description: r.Description || r.description || '',
      severity: r.Severity || r.severity || 'low',
      suggestion: r.Suggestion || r.suggestion || ''
    }));

    return {
      tone: tone,
      questionsCovered: questionsCovered,
      questionsIgnored: draft.QuestionsIgnored || draft.questionsIgnored || [],
      newQuestionsIntroduced: draft.NewQuestionsIntroduced || draft.newQuestionsIntroduced || [],
      riskFlags: riskFlags,
      completenessScore: draft.CompletenessScore ?? draft.completenessScore ?? 0,
      suggestions: draft.Suggestions || draft.suggestions || [],
      overallAssessment: draft.OverallAssessment || draft.overallAssessment || '',
      readyToSend: draft.ReadyToSend ?? draft.readyToSend ?? false
    };
  }

  toggleSpellCheck() {
    this.spellCheckEnabled = !this.spellCheckEnabled;

    if (this.spellCheckEnabled && this.results?.Messages?.length > 0) {
      this.runSpellCheck();
    } else {
      this.spellCheckResults.clear();
      this.totalSpellIssues = 0;
    }
  }

  // Add this method to run spell check on all messages
  runSpellCheck() {
    if (!this.results?.Messages || this.results.Messages.length === 0) {
      return;
    }

    this.spellCheckLoading = true;

    // Prepare messages for spell check
    const messagesToCheck = this.results.Messages.map((msg: any, index: number) => ({
      messageId: msg.Id || msg.id || `msg-${index}`,
      text: msg.Content || msg.content || ''
    })).filter((m: any) => m.text.length > 0);

    this.apiService.checkMessagesSpelling(messagesToCheck)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.spellCheckResults.clear();
          this.totalSpellIssues = 0;

          if (response.success && response.results) {
            response.results.forEach(result => {
              if (result.issues && result.issues.length > 0) {
                this.spellCheckResults.set(result.messageId, result.issues);
                this.totalSpellIssues += result.issues.length;
              }
            });
          }

          this.spellCheckLoading = false;
          console.log(`Spell check complete: ${this.totalSpellIssues} issues found`);
        },
        error: (err) => {
          console.error('Spell check error:', err);
          this.spellCheckLoading = false;
        }
      });
  }

  // Add this helper method to get issues for a specific message
  getMessageSpellIssues(messageId: string): SpellCheckIssue[] {
    return this.spellCheckResults.get(messageId) || [];
  }

  // Add this helper to check if a message has issues
  hasSpellIssues(messageId: string): boolean {
    const issues = this.spellCheckResults.get(messageId);
    return issues !== undefined && issues.length > 0;
  }

  clear() {
    this.conversationText = '';
    this.draftMessage = '';
    this.results = null;
    this.draftAnalysis = null;
    this.error = '';
    this.selectedImages = [];
    this.imagePreviews = [];
    this.selectedAudio = null;
    this.streamingProgress = 0;
    this.streamStatus = '';
    this.insightStored = false;

    Object.keys(this.sectionsLoading).forEach(key => {
      this.sectionsLoading[key] = false;
      this.sectionsComplete[key] = false;
      this.sectionsError[key] = '';
    });
  }

  goToAdmin() {
    this.router.navigate(['/admin']);
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
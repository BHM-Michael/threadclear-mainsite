import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-conversation-analyzer',
  templateUrl: './conversation-analyzer.component.html',
  styleUrls: ['./conversation-analyzer.component.scss']
})
export class ConversationAnalyzerComponent implements OnInit, OnDestroy {
  conversationText = '';
  draftMessage = '';  // NEW: Draft message input
  sourceType = 'simple';
  parsingMode = 2;
  loading = false;
  results: any = null;
  draftAnalysis: any = null;  // NEW: Draft analysis results
  error = '';
  loadingMessage = 'Analyzing conversation...';

  // Image upload - supports multiple
  inputMode: 'text' | 'image' | 'audio' = 'text';
  selectedImages: File[] = [];
  selectedAudio: File | null = null;
  imagePreviews: string[] = [];

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

    // Listen for paste events
    document.addEventListener('paste', this.onPaste.bind(this));
  }

  ngOnDestroy() {
    // Clean up paste listener
    document.removeEventListener('paste', this.onPaste.bind(this));
  }

  onPaste(event: ClipboardEvent) {
    const items = event.clipboardData?.items;
    if (!items) return;

    for (let i = 0; i < items.length; i++) {
      if (items[i].type.startsWith('image/')) {
        event.preventDefault();
        const file = items[i].getAsFile();
        if (file) {
          // Switch to image mode and add the pasted image
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
    // Check file size (max 25MB for Whisper)
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
    } else {
      this.analyzeText();
    }
  }

  analyzeText() {
    this.loading = true;
    this.loadingMessage = 'Parsing conversation...';
    this.startProgressMessages();
    this.error = '';
    this.results = null;
    this.draftAnalysis = null;  // Reset draft analysis

    const perms = this.permissions;
    const isAdmin = this.isAdmin;

    // Build request with permissions
    const request: any = {
      conversationText: this.conversationText,
      sourceType: this.sourceType,
      parsingMode: this.parsingMode,
      draftMessage: this.draftMessage || null  // NEW: Include draft message
    };

    // If not admin, include permission flags
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
    this.loadingMessage = 'Parsing conversation...';
    this.startProgressMessages();
    this.error = '';
    this.results = null;
    this.draftAnalysis = null;

    const perms = this.permissions;
    const isAdmin = this.isAdmin;

    // Build permission flags
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

    this.apiService.analyzeImages(this.selectedImages, this.sourceType, this.parsingMode, enableFlags)
      .subscribe({
        next: (response) => {
          this.results = this.filterResultsByPermissions(response.capsule);
          this.draftAnalysis = this.normalizeDraftAnalysis(response.draftAnalysis);
          this.loading = false;
        },
        error: (err) => {
          this.error = 'Error analyzing images. Please try again.';
          this.loading = false;
          console.error(err);
        }
      });
  }

  analyzeAudio() {
    if (!this.selectedAudio) return;

    this.loadingMessage = 'Parsing conversation...';
    this.startProgressMessages();
    this.error = '';
    this.results = null;
    this.draftAnalysis = null;

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
      .subscribe({
        next: (response) => {
          this.results = this.filterResultsByPermissions(response.capsule);
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
      return {}; // Admin gets all features
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
    if (!capsule) {
      return capsule;
    }

    console.log("Filtering results. isAdmin:", this.isAdmin, "permissions:", this.permissions);

    if (this.isAdmin || !this.permissions) {
      return capsule; // Admin sees everything
    }

    const perms = this.permissions;

    // Check both casings for permissions
    const hasUnanswered = perms.UnansweredQuestions || perms.unansweredQuestions || false;
    const hasTension = perms.TensionPoints || perms.tensionPoints || false;
    const hasMisalignments = perms.Misalignments || perms.misalignments || false;
    const hasHealth = perms.ConversationHealth || perms.conversationHealth || false;
    const hasSuggested = perms.SuggestedActions || perms.suggestedActions || false;

    console.log("Permission checks:", { hasUnanswered, hasTension, hasMisalignments, hasHealth, hasSuggested });

    // Filter out features user doesn't have access to - check both casings for capsule properties
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

    console.log("Filtered capsule:", capsule);

    return capsule;
  }

  // Normalize draft analysis to handle PascalCase from API
  // Normalize draft analysis to handle PascalCase from API
  normalizeDraftAnalysis(draft: any): any {
    if (!draft) return null;

    // Normalize tone object
    const toneRaw = draft.Tone || draft.tone;
    const tone = toneRaw ? {
      tone: toneRaw.Tone || toneRaw.tone || '',
      matchesConversationTone: toneRaw.MatchesConversationTone ?? toneRaw.matchesConversationTone ?? true,
      escalationRisk: toneRaw.EscalationRisk || toneRaw.escalationRisk || 'none',
      explanation: toneRaw.Explanation || toneRaw.explanation || ''
    } : null;

    // Normalize questions covered
    const questionsCoveredRaw = draft.QuestionsCovered || draft.questionsCovered || [];
    const questionsCovered = questionsCoveredRaw.map((q: any) => ({
      question: q.Question || q.question || '',
      addressed: q.Addressed ?? q.addressed ?? false,
      howAddressed: q.HowAddressed || q.howAddressed || null
    }));

    // Normalize risk flags
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

  clear() {
    this.conversationText = '';
    this.draftMessage = '';  // NEW: Clear draft message
    this.results = null;
    this.draftAnalysis = null;  // NEW: Clear draft analysis
    this.error = '';
    this.selectedImages = [];
    this.imagePreviews = [];
    this.selectedAudio = null;
  }

  goToAdmin() {
    this.router.navigate(['/admin']);
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
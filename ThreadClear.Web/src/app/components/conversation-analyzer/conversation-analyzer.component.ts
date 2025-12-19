import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-conversation-analyzer',
  templateUrl: './conversation-analyzer.component.html',
  styleUrls: ['./conversation-analyzer.component.scss']
})
export class ConversationAnalyzerComponent implements OnInit {
  conversationText = '';
  sourceType = 'simple';
  parsingMode = 2;
  loading = false;
  results: any = null;
  error = '';

  // Image upload - supports multiple
  inputMode: 'text' | 'image' = 'text';
  selectedImages: File[] = [];
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
    } else {
      this.analyzeText();
    }
  }

  analyzeText() {
    this.loading = true;
    this.error = '';
    this.results = null;

    // Build permissions object to send to API
    const enabledFeatures = this.getEnabledFeatures();

    this.apiService.analyzeConversation({
      conversationText: this.conversationText,
      sourceType: this.sourceType,
      parsingMode: this.parsingMode,
      ...enabledFeatures
    }).subscribe({
      next: (response) => {
        this.results = this.filterResultsByPermissions(response.capsule);
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
    this.error = '';
    this.results = null;

    this.apiService.analyzeImages(this.selectedImages, this.sourceType, this.parsingMode)
      .subscribe({
        next: (response) => {
          this.results = this.filterResultsByPermissions(response.capsule);
          this.loading = false;
        },
        error: (err) => {
          this.error = 'Error analyzing images. Please try again.';
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

  clear() {
    this.conversationText = '';
    this.results = null;
    this.error = '';
    this.selectedImages = [];
    this.imagePreviews = [];
  }

  goToAdmin() {
    this.router.navigate(['/admin']);
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
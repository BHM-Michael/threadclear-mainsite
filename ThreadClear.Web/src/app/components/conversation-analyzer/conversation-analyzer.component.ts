import { Component } from '@angular/core';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-conversation-analyzer',
  templateUrl: './conversation-analyzer.component.html',
  styleUrls: ['./conversation-analyzer.component.scss']
})
export class ConversationAnalyzerComponent {
  conversationText = '';
  sourceType = 'simple';
  parsingMode = 2;
  loading = false;
  results: any = null;
  error = '';

  // Image upload
  inputMode: 'text' | 'image' = 'text';
  selectedImage: File | null = null;
  imagePreview: string | null = null;

  constructor(private apiService: ApiService) { }

  onFileSelected(event: any) {
    const file = event.target.files[0];
    if (file) {
      this.selectImage(file);
    }
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
      this.selectImage(files[0]);
    }
  }

  selectImage(file: File) {
    if (!file.type.startsWith('image/')) {
      this.error = 'Please select an image file';
      return;
    }

    // Check file size (max 10MB)
    if (file.size > 10 * 1024 * 1024) {
      this.error = 'Image must be less than 10MB';
      return;
    }

    this.selectedImage = file;
    this.error = '';

    // Create preview
    const reader = new FileReader();
    reader.onload = (e) => {
      this.imagePreview = e.target?.result as string;
    };
    reader.readAsDataURL(file);
  }

  removeImage(event: Event) {
    event.stopPropagation();
    this.selectedImage = null;
    this.imagePreview = null;
  }

  analyze() {
    if (this.inputMode === 'image' && this.selectedImage) {
      this.analyzeImage();
    } else {
      this.analyzeText();
    }
  }

  analyzeText() {
    this.loading = true;
    this.error = '';
    this.results = null;

    this.apiService.analyzeConversation({
      conversationText: this.conversationText,
      sourceType: this.sourceType,
      parsingMode: this.parsingMode
    }).subscribe({
      next: (response) => {
        this.results = response.capsule;
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Error analyzing conversation. Please try again.';
        this.loading = false;
        console.error(err);
      }
    });
  }

  analyzeImage() {
    if (!this.selectedImage) return;

    this.loading = true;
    this.error = '';
    this.results = null;

    this.apiService.analyzeImage(this.selectedImage, this.sourceType, this.parsingMode)
      .subscribe({
        next: (response) => {
          this.results = response.capsule;
          this.loading = false;
        },
        error: (err) => {
          this.error = 'Error analyzing image. Please try again.';
          this.loading = false;
          console.error(err);
        }
      });
  }

  clear() {
    this.conversationText = '';
    this.results = null;
    this.error = '';
    this.selectedImage = null;
    this.imagePreview = null;
  }
}
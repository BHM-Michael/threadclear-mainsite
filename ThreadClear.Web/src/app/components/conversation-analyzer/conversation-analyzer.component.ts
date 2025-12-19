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

  // Image upload - now supports multiple
  inputMode: 'text' | 'image' = 'text';
  selectedImages: File[] = [];
  imagePreviews: string[] = [];

  constructor(private apiService: ApiService) { }

  onFileSelected(event: any) {
    const files = event.target.files;
    if (files) {
      this.addImages(Array.from(files));
    }
    // Reset input so same file can be selected again
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

      // Check file size (max 10MB per image)
      if (file.size > 10 * 1024 * 1024) {
        this.error = 'Each image must be less than 10MB';
        continue;
      }

      // Max 10 images
      if (this.selectedImages.length >= 10) {
        this.error = 'Maximum 10 images allowed';
        break;
      }

      this.selectedImages.push(file);
      this.error = '';

      // Create preview
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

  analyzeImages() {
    this.loading = true;
    this.error = '';
    this.results = null;

    this.apiService.analyzeImages(this.selectedImages, this.sourceType, this.parsingMode)
      .subscribe({
        next: (response) => {
          this.results = response.capsule;
          this.loading = false;
        },
        error: (err) => {
          this.error = 'Error analyzing images. Please try again.';
          this.loading = false;
          console.error(err);
        }
      });
  }

  clear() {
    this.conversationText = '';
    this.results = null;
    this.error = '';
    this.selectedImages = [];
    this.imagePreviews = [];
  }
}
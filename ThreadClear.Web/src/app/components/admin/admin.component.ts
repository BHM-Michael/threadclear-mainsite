import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService, User, CreateUserRequest, UserPermissions } from '../../services/auth.service';

@Component({
  selector: 'app-admin',
  templateUrl: './admin.component.html',
  styleUrls: ['./admin.component.scss']
})
export class AdminComponent implements OnInit {
  users: any[] = [];
  pricing: any[] = [];
  loading = false;
  error = '';
  success = '';

  // New user form
  showNewUserForm = false;
  newUser: CreateUserRequest = {
    email: '',
    password: '',
    unansweredQuestions: true,
    tensionPoints: true,
    misalignments: true,
    conversationHealth: true,
    suggestedActions: true
  };

  // Edit user
  editingUser: any | null = null;

  constructor(
    private authService: AuthService,
    private router: Router
  ) { }

  ngOnInit() {
    if (!this.authService.isAdmin) {
      this.router.navigate(['/analyze']);
      return;
    }
    this.loadUsers();
    this.loadPricing();
  }

  loadUsers() {
    this.loading = true;
    this.authService.getUsers().subscribe({
      next: (response) => {
        this.users = response.users;
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Failed to load users';
        this.loading = false;
        console.error(err);
      }
    });
  }

  loadPricing() {
    this.authService.getFeaturePricing().subscribe({
      next: (response) => {
        this.pricing = response.pricing;
      },
      error: (err) => {
        console.error('Failed to load pricing', err);
      }
    });
  }

  createUser() {
    if (!this.newUser.email || !this.newUser.password) {
      this.error = 'Email and password are required';
      return;
    }

    this.loading = true;
    this.error = '';

    this.authService.createUser(this.newUser).subscribe({
      next: (response) => {
        this.loading = false;
        this.success = 'User created successfully';
        this.showNewUserForm = false;
        this.resetNewUserForm();
        this.loadUsers();
        setTimeout(() => this.success = '', 3000);
      },
      error: (err) => {
        this.loading = false;
        this.error = err.error?.error || 'Failed to create user';
        console.error(err);
      }
    });
  }

  resetNewUserForm() {
    this.newUser = {
      email: '',
      password: '',
      unansweredQuestions: true,
      tensionPoints: true,
      misalignments: true,
      conversationHealth: true,
      suggestedActions: true
    };
  }

  editUser(user: User) {
    this.editingUser = { ...user, permissions: { ...user.permissions } };
  }

  savePermissions() {
    if (!this.editingUser) return;

    this.loading = true;
    this.authService.updateUserPermissions(this.editingUser.Id, this.editingUser.Permissions).subscribe({
      next: () => {
        this.loading = false;
        this.success = 'Permissions updated';
        this.editingUser = null;
        this.loadUsers();
        setTimeout(() => this.success = '', 3000);
      },
      error: (err) => {
        this.loading = false;
        this.error = 'Failed to update permissions';
        console.error(err);
      }
    });
  }

  cancelEdit() {
    this.editingUser = null;
  }

  deleteUser(user: User) {
    if (!confirm(`Are you sure you want to delete ${user.email}?`)) {
      return;
    }

    this.authService.deleteUser(user.id).subscribe({
      next: () => {
        this.success = 'User deleted';
        this.loadUsers();
        setTimeout(() => this.success = '', 3000);
      },
      error: (err) => {
        this.error = 'Failed to delete user';
        console.error(err);
      }
    });
  }

  updatePricing(feature: any) {
    this.authService.updateFeaturePricing(feature.FeatureName, feature.PricePerUse).subscribe({
      next: () => {
        this.success = 'Pricing updated';
        setTimeout(() => this.success = '', 3000);
      },
      error: (err) => {
        this.error = 'Failed to update pricing';
        console.error(err);
      }
    });
  }

  goToAnalyzer() {
    this.router.navigate(['/analyze']);
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  getFeatureDisplayName(featureName: string): string {
    const names: { [key: string]: string } = {
      'UnansweredQuestions': 'Unanswered Questions',
      'TensionPoints': 'Tension Points',
      'Misalignments': 'Misalignments',
      'ConversationHealth': 'Conversation Health',
      'SuggestedActions': 'Suggested Actions'
    };
    return names[featureName] || featureName;
  }
}
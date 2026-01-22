import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService, User, CreateUserRequest, UserPermissions } from '../../services/auth.service';
import { AdminService, AdminOrganization, AdminOrgMember, CreateOrgRequest, InviteUserRequest } from '../../services/admin.service';
import { TaxonomyService, TaxonomyData, TopicDefinition, RoleDefinition, IndustryType } from '../../services/taxonomy.service';
import { OrganizationService } from '../../services/organization.service';

@Component({
  selector: 'app-admin',
  templateUrl: './admin.component.html',
  styleUrls: ['./admin.component.scss']
})
export class AdminComponent implements OnInit {
  // Tab management
  activeTab: 'users' | 'organizations' = 'users';

  users: any[] = [];
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

  // Organizations
  organizations: AdminOrganization[] = [];
  myOrgIds: Set<string> = new Set();
  showNewOrgForm = false;
  newOrg: CreateOrgRequest = {
    name: '',
    ownerEmail: '',
    industryType: 'default',
    plan: 'professional'
  };

  // Organization details
  selectedOrg: AdminOrganization | null = null;
  selectedOrgMembers: AdminOrgMember[] = [];

  // Invite user
  showInviteForm = false;
  inviteRequest: InviteUserRequest = {
    email: '',
    role: 'Member'
  };

  // Bulk invite
  showBulkInviteForm = false;
  bulkEmails = '';
  bulkDefaultRole = 'Member';

  // Organization settings (when viewing an org)
  orgSettingsTab: 'members' | 'general' | 'taxonomy' = 'members';
  taxonomy: TaxonomyData | null = null;
  orgName = '';
  orgIndustry = '';
  isSaving = false;

  // Taxonomy forms
  newTopicKey = '';
  newTopicName = '';
  newTopicKeywords = '';
  newRoleKey = '';
  newRoleName = '';
  newRoleKeywords = '';

  // Industry options

  // Industry options
  industries = [
    { key: 'default', name: 'Default' },
    { key: 'legal', name: 'Legal' },
    { key: 'healthcare', name: 'Healthcare' },
    { key: 'finance', name: 'Finance' },
    { key: 'technology', name: 'Technology' },
    { key: 'retail', name: 'Retail' }
  ];

  // Plan options
  plans = [
    { key: 'free', name: 'Free' },
    { key: 'starter', name: 'Starter' },
    { key: 'professional', name: 'Professional' },
    { key: 'enterprise', name: 'Enterprise' }
  ];

  constructor(
    private authService: AuthService,
    private adminService: AdminService,
    private taxonomyService: TaxonomyService,
    private orgService: OrganizationService,
    private router: Router
  ) { }

  ngOnInit() {
    if (!this.authService.isAdmin) {
      this.router.navigate(['/analyze']);
      return;
    }
    this.loadUsers();
    this.loadOrganizations();
    this.loadMyOrganizations();  // ADD THIS
  }

  setTab(tab: 'users' | 'organizations') {
    this.activeTab = tab;
    this.clearMessages();
  }

  clearMessages() {
    this.error = '';
    this.success = '';
  }

  // ============================================
  // USERS
  // ============================================

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
      error: (err: any) => {
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
    this.editingUser = { ...user, Permissions: { ...user.permissions } };
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
      error: (err: any) => {
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
      error: (err: any) => {
        this.error = 'Failed to delete user';
        console.error(err);
      }
    });
  }

  // ============================================
  // ORGANIZATIONS
  // ============================================

  loadOrganizations() {
    this.adminService.getOrganizations().subscribe({
      next: (response) => {
        if (response.success) {
          this.organizations = response.organizations;
        }
      },
      error: (err) => {
        console.error('Failed to load organizations', err);
      }
    });
  }

  loadMyOrganizations() {
    this.orgService.getMyOrganizations().subscribe({
      next: (response) => {
        if (response.success && response.organizations) {
          this.myOrgIds = new Set(response.organizations.map(o => o.id));
        }
      },
      error: (err) => console.error('Failed to load my organizations', err)
    });
  }

  isMyOrg(orgId: string): boolean {
    return this.myOrgIds.has(orgId);
  }

  createOrganization() {
    if (!this.newOrg.name || !this.newOrg.ownerEmail) {
      this.error = 'Organization name and owner email are required';
      return;
    }

    this.loading = true;
    this.clearMessages();

    this.adminService.createOrganization(this.newOrg).subscribe({
      next: (response) => {
        this.loading = false;
        if (response.success) {
          this.success = `Organization created! Invite URL: ${response.inviteUrl}`;
          this.showNewOrgForm = false;
          this.resetNewOrgForm();
          this.loadOrganizations();

          // Copy invite URL to clipboard
          if (response.inviteUrl) {
            navigator.clipboard.writeText(response.inviteUrl);
            this.success += ' (Copied to clipboard)';
          }
        }
      },
      error: (err) => {
        this.loading = false;
        this.error = err.error?.error || 'Failed to create organization';
      }
    });
  }

  resetNewOrgForm() {
    this.newOrg = {
      name: '',
      ownerEmail: '',
      industryType: 'default',
      plan: 'professional'
    };
  }

  viewOrganization(org: AdminOrganization) {
    this.selectedOrg = org;
    this.orgName = org.name;
    this.orgIndustry = org.industryType || 'default';
    this.orgSettingsTab = 'members';
    this.loadOrgMembers(org.id);
    this.loadOrgTaxonomy(org.id);
  }

  loadOrgMembers(orgId: string) {
    this.adminService.getOrganization(orgId).subscribe({
      next: (response) => {
        if (response.success) {
          this.selectedOrgMembers = response.members;
        }
      },
      error: (err) => {
        this.error = 'Failed to load organization details';
        console.error(err);
      }
    });
  }

  closeOrgDetails() {
    this.selectedOrg = null;
    this.selectedOrgMembers = [];
    this.showInviteForm = false;
    this.showBulkInviteForm = false;
    this.taxonomy = null;
    this.orgSettingsTab = 'members';
  }

  setOrgSettingsTab(tab: 'members' | 'general' | 'taxonomy') {
    this.orgSettingsTab = tab;
  }

  loadOrgTaxonomy(orgId: string) {
    this.taxonomyService.getOrganizationTaxonomy(orgId).subscribe({
      next: (response) => {
        if (response.success) {
          this.taxonomy = response.taxonomy;
        }
      },
      error: (err) => console.error('Failed to load taxonomy', err)
    });
  }

  saveOrgSettings() {
    if (!this.selectedOrg) return;
    this.isSaving = true;
    this.clearMessages();

    this.orgService.updateOrganization(this.selectedOrg.id, {
      name: this.orgName,
      industryType: this.orgIndustry
    }).subscribe({
      next: (response: any) => {
        this.isSaving = false;
        if (response.success) {
          this.success = 'Organization settings saved';
          this.selectedOrg!.name = this.orgName;
          this.loadOrganizations();
          setTimeout(() => this.success = '', 3000);
        }
      },
      error: (err: any) => {
        this.isSaving = false;
        this.error = err.error?.error || 'Failed to save settings';
      }
    });
  }

  addTopic() {
    if (!this.selectedOrg || !this.newTopicKey || !this.newTopicName) return;

    const topic: TopicDefinition = {
      key: this.newTopicKey.toLowerCase().replace(/\s+/g, '_'),
      displayName: this.newTopicName,
      keywords: this.newTopicKeywords.split(',').map(k => k.trim()).filter(k => k),
      isCustom: true
    };

    this.isSaving = true;
    this.taxonomyService.addCustomTopic(this.selectedOrg.id, topic).subscribe({
      next: (response) => {
        this.isSaving = false;
        if (response.success && this.taxonomy) {
          this.taxonomy.topics.push(topic);
          this.newTopicKey = '';
          this.newTopicName = '';
          this.newTopicKeywords = '';
          this.success = 'Topic added';
          setTimeout(() => this.success = '', 3000);
        }
      },
      error: () => {
        this.isSaving = false;
        this.error = 'Failed to add topic';
      }
    });
  }

  removeTopic(topic: TopicDefinition) {
    if (!this.selectedOrg || !topic.isCustom) return;
    if (!confirm(`Remove topic "${topic.displayName}"?`)) return;

    this.taxonomyService.removeCustomTopic(this.selectedOrg.id, topic.key).subscribe({
      next: () => {
        if (this.taxonomy) {
          this.taxonomy.topics = this.taxonomy.topics.filter(t => t.key !== topic.key);
        }
        this.success = 'Topic removed';
        setTimeout(() => this.success = '', 3000);
      },
      error: () => {
        this.error = 'Failed to remove topic';
      }
    });
  }

  addRole() {
    if (!this.selectedOrg || !this.newRoleKey || !this.newRoleName) return;

    const role: RoleDefinition = {
      key: this.newRoleKey.toLowerCase().replace(/\s+/g, '_'),
      displayName: this.newRoleName,
      keywords: this.newRoleKeywords.split(',').map(k => k.trim()).filter(k => k)
    };

    this.isSaving = true;
    this.taxonomyService.addCustomRole(this.selectedOrg.id, role).subscribe({
      next: (response) => {
        this.isSaving = false;
        if (response.success && this.taxonomy) {
          this.taxonomy.roles.push(role);
          this.newRoleKey = '';
          this.newRoleName = '';
          this.newRoleKeywords = '';
          this.success = 'Role added';
          setTimeout(() => this.success = '', 3000);
        }
      },
      error: () => {
        this.isSaving = false;
        this.error = 'Failed to add role';
      }
    });
  }

  removeRole(role: RoleDefinition) {
    if (!this.selectedOrg) return;
    if (!confirm(`Remove role "${role.displayName}"?`)) return;

    this.taxonomyService.removeCustomRole(this.selectedOrg.id, role.key).subscribe({
      next: () => {
        if (this.taxonomy) {
          this.taxonomy.roles = this.taxonomy.roles.filter(r => r.key !== role.key);
        }
        this.success = 'Role removed';
        setTimeout(() => this.success = '', 3000);
      },
      error: () => {
        this.error = 'Failed to remove role';
      }
    });
  }

  // Invite single user
  inviteUser() {
    if (!this.selectedOrg || !this.inviteRequest.email) {
      this.error = 'Email is required';
      return;
    }

    this.loading = true;
    this.clearMessages();

    this.adminService.inviteUser(this.selectedOrg.id, this.inviteRequest).subscribe({
      next: (response) => {
        this.loading = false;
        if (response.success) {
          this.success = `Invite sent! URL: ${response.inviteUrl}`;
          this.showInviteForm = false;
          this.inviteRequest = { email: '', role: 'Member' };
          this.loadOrgMembers(this.selectedOrg!.id);

          // Copy to clipboard
          if (response.inviteUrl) {
            navigator.clipboard.writeText(response.inviteUrl);
            this.success += ' (Copied to clipboard)';
          }
        }
      },
      error: (err) => {
        this.loading = false;
        this.error = err.error?.error || 'Failed to invite user';
      }
    });
  }

  // Bulk invite
  bulkInvite() {
    if (!this.selectedOrg || !this.bulkEmails.trim()) {
      this.error = 'Enter at least one email';
      return;
    }

    const emails = this.bulkEmails
      .split(/[\n,;]/)
      .map(e => e.trim())
      .filter(e => e && e.includes('@'));

    if (emails.length === 0) {
      this.error = 'No valid emails found';
      return;
    }

    this.loading = true;
    this.clearMessages();

    const users = emails.map(email => ({ email, role: this.bulkDefaultRole }));

    this.adminService.bulkInvite(this.selectedOrg.id, users, this.bulkDefaultRole).subscribe({
      next: (response) => {
        this.loading = false;
        if (response.success) {
          this.success = `Bulk invite complete: ${response.summary.succeeded}/${response.summary.total} succeeded`;
          this.showBulkInviteForm = false;
          this.bulkEmails = '';
          this.loadOrgMembers(this.selectedOrg!.id);

          // Show results in console for debugging
          console.log('Bulk invite results:', response.results);
        }
      },
      error: (err) => {
        this.loading = false;
        this.error = err.error?.error || 'Failed to process bulk invite';
      }
    });
  }

  // Resend invite
  resendInvite(member: AdminOrgMember) {
    if (!this.selectedOrg) return;

    this.adminService.resendInvite(this.selectedOrg.id, member.userId).subscribe({
      next: (response) => {
        if (response.success) {
          this.success = `Invite resent! URL: ${response.inviteUrl}`;

          // Copy to clipboard
          if (response.inviteUrl) {
            navigator.clipboard.writeText(response.inviteUrl);
            this.success += ' (Copied to clipboard)';
          }
          setTimeout(() => this.success = '', 5000);
        }
      },
      error: (err) => {
        this.error = err.error?.error || 'Failed to resend invite';
      }
    });
  }

  copyInviteUrl(token: string) {
    const url = `${window.location.origin}/register?invite=${token}`;
    navigator.clipboard.writeText(url);
    this.success = 'Invite URL copied to clipboard';
    setTimeout(() => this.success = '', 3000);
  }


  // ============================================
  // NAVIGATION
  // ============================================

  goToAnalyzer() {
    this.router.navigate(['/analyze']);
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
  getMemberStatusClass(status: string): string {
    switch (status) {
      case 'Active': return 'status-active';
      case 'Invited': return 'status-invited';
      case 'Suspended': return 'status-suspended';
      default: return '';
    }
  }
}

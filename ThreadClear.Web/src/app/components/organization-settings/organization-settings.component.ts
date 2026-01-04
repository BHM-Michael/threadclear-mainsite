import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
import { OrganizationService, Organization, OrganizationMember } from '../../services/organization.service';
import { TaxonomyService, TaxonomyData, TopicDefinition, RoleDefinition, IndustryType } from '../../services/taxonomy.service';

@Component({
  selector: 'app-organization-settings',
  templateUrl: './organization-settings.component.html',
  styleUrls: ['./organization-settings.component.scss']
})
export class OrganizationSettingsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  currentOrg: Organization | null = null;
  members: OrganizationMember[] = [];
  taxonomy: TaxonomyData | null = null;
  industries: IndustryType[] = [];

  activeTab: 'general' | 'members' | 'taxonomy' = 'general';
  
  isLoading = true;
  isSaving = false;
  error: string | null = null;
  successMessage: string | null = null;

  // Forms
  orgName = '';
  orgIndustry = '';
  
  inviteEmail = '';
  inviteRole = 'Member';

  newTopicKey = '';
  newTopicName = '';
  newTopicKeywords = '';

  newRoleKey = '';
  newRoleName = '';
  newRoleKeywords = '';

  constructor(
    private orgService: OrganizationService,
    private taxonomyService: TaxonomyService
  ) {}

  ngOnInit(): void {
    // Load industries
    this.taxonomyService.getIndustryTypes().subscribe({
      next: (response) => {
        if (response.success) {
          this.industries = response.industries;
        }
      }
    });

    this.orgService.currentOrg$
      .pipe(takeUntil(this.destroy$))
      .subscribe(org => {
        this.currentOrg = org;
        if (org) {
          this.orgName = org.name;
          this.orgIndustry = org.industryType;
          this.loadOrgData();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadOrgData(): void {
    if (!this.currentOrg) return;

    this.isLoading = true;
    const orgId = this.currentOrg.id;

    // Load members
    this.orgService.getMembers(orgId).subscribe({
      next: (response) => {
        if (response.success) {
          this.members = response.members;
        }
      }
    });

    // Load taxonomy
    this.taxonomyService.getOrganizationTaxonomy(orgId).subscribe({
      next: (response) => {
        if (response.success) {
          this.taxonomy = response.taxonomy;
        }
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  setTab(tab: 'general' | 'members' | 'taxonomy'): void {
    this.activeTab = tab;
    this.clearMessages();
  }

  clearMessages(): void {
    this.error = null;
    this.successMessage = null;
  }

  // General Settings
  saveGeneralSettings(): void {
    if (!this.currentOrg) return;

    this.isSaving = true;
    this.clearMessages();

    this.orgService.updateOrganization(this.currentOrg.id, {
      name: this.orgName,
      industryType: this.orgIndustry
    }).subscribe({
      next: (response) => {
        if (response.success) {
          this.successMessage = 'Settings saved successfully';
          // Reload taxonomy if industry changed
          if (this.orgIndustry !== this.currentOrg?.industryType) {
            this.loadOrgData();
          }
        }
        this.isSaving = false;
      },
      error: (err) => {
        this.error = 'Failed to save settings';
        this.isSaving = false;
      }
    });
  }

  // Members
  inviteMember(): void {
    if (!this.currentOrg || !this.inviteEmail) return;

    this.isSaving = true;
    this.clearMessages();

    this.orgService.inviteMember(this.currentOrg.id, {
      email: this.inviteEmail,
      role: this.inviteRole
    }).subscribe({
      next: (response) => {
        if (response.success) {
          this.successMessage = `Invite sent to ${this.inviteEmail}`;
          this.inviteEmail = '';
          this.loadOrgData();
        }
        this.isSaving = false;
      },
      error: (err) => {
        this.error = err.error?.error || 'Failed to send invite';
        this.isSaving = false;
      }
    });
  }

  updateMemberRole(member: OrganizationMember, newRole: string): void {
    if (!this.currentOrg) return;

    this.orgService.updateMemberRole(this.currentOrg.id, member.userId, newRole).subscribe({
      next: () => {
        member.role = newRole;
        this.successMessage = 'Role updated';
      },
      error: (err) => {
        this.error = err.error?.error || 'Failed to update role';
      }
    });
  }

  removeMember(member: OrganizationMember): void {
    if (!this.currentOrg) return;
    if (!confirm(`Remove ${member.email} from the organization?`)) return;

    this.orgService.removeMember(this.currentOrg.id, member.userId).subscribe({
      next: () => {
        this.members = this.members.filter(m => m.userId !== member.userId);
        this.successMessage = 'Member removed';
      },
      error: (err) => {
        this.error = err.error?.error || 'Failed to remove member';
      }
    });
  }

  // Taxonomy - Topics
  addTopic(): void {
    if (!this.currentOrg || !this.newTopicKey || !this.newTopicName) return;

    const topic: TopicDefinition = {
      key: this.newTopicKey.toLowerCase().replace(/\s+/g, '_'),
      displayName: this.newTopicName,
      keywords: this.newTopicKeywords.split(',').map(k => k.trim()).filter(k => k),
      isCustom: true
    };

    this.isSaving = true;
    this.taxonomyService.addCustomTopic(this.currentOrg.id, topic).subscribe({
      next: (response) => {
        if (response.success && this.taxonomy) {
          this.taxonomy.topics.push(topic);
          this.newTopicKey = '';
          this.newTopicName = '';
          this.newTopicKeywords = '';
          this.successMessage = 'Topic added';
        }
        this.isSaving = false;
      },
      error: () => {
        this.error = 'Failed to add topic';
        this.isSaving = false;
      }
    });
  }

  removeTopic(topic: TopicDefinition): void {
    if (!this.currentOrg || !topic.isCustom) return;
    if (!confirm(`Remove topic "${topic.displayName}"?`)) return;

    this.taxonomyService.removeCustomTopic(this.currentOrg.id, topic.key).subscribe({
      next: () => {
        if (this.taxonomy) {
          this.taxonomy.topics = this.taxonomy.topics.filter(t => t.key !== topic.key);
        }
        this.successMessage = 'Topic removed';
      },
      error: () => {
        this.error = 'Failed to remove topic';
      }
    });
  }

  // Taxonomy - Roles
  addRole(): void {
    if (!this.currentOrg || !this.newRoleKey || !this.newRoleName) return;

    const role: RoleDefinition = {
      key: this.newRoleKey.toLowerCase().replace(/\s+/g, '_'),
      displayName: this.newRoleName,
      keywords: this.newRoleKeywords.split(',').map(k => k.trim()).filter(k => k)
    };

    this.isSaving = true;
    this.taxonomyService.addCustomRole(this.currentOrg.id, role).subscribe({
      next: (response) => {
        if (response.success && this.taxonomy) {
          this.taxonomy.roles.push(role);
          this.newRoleKey = '';
          this.newRoleName = '';
          this.newRoleKeywords = '';
          this.successMessage = 'Role added';
        }
        this.isSaving = false;
      },
      error: () => {
        this.error = 'Failed to add role';
        this.isSaving = false;
      }
    });
  }

  removeRole(role: RoleDefinition): void {
    if (!this.currentOrg) return;
    if (!confirm(`Remove role "${role.displayName}"?`)) return;

    this.taxonomyService.removeCustomRole(this.currentOrg.id, role.key).subscribe({
      next: () => {
        if (this.taxonomy) {
          this.taxonomy.roles = this.taxonomy.roles.filter(r => r.key !== role.key);
        }
        this.successMessage = 'Role removed';
      },
      error: () => {
        this.error = 'Failed to remove role';
      }
    });
  }

  getRoleBadgeClass(role: string): string {
    switch (role) {
      case 'Owner': return 'badge-owner';
      case 'Admin': return 'badge-admin';
      case 'Analyst': return 'badge-analyst';
      default: return 'badge-member';
    }
  }
}

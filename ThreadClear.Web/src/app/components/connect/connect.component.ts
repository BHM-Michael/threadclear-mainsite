import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { environment } from '../../../environments/environment';

interface WorkspaceInfo {
  platform: string;
  id: string;
  name: string;
  tier: string;
  usage: number;
  limit: number;
  isConnected: boolean;
  connectedOrgName?: string;
}

@Component({
  selector: 'app-connect',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './connect.component.html',
  styleUrls: ['./connect.component.scss']
})
export class ConnectComponent implements OnInit {
  loading = true;
  error: string | null = null;
  success = false;
  connecting = false;

  platform: string = '';
  workspaceId: string = '';
  workspace: WorkspaceInfo | null = null;

  isLoggedIn = false;
  userEmail: string = '';
  orgName: string = '';
  hasOrg = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private http: HttpClient
  ) { }

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      this.platform = params['platform'] || '';
      this.workspaceId = params['id'] || '';

      if (!this.platform || !this.workspaceId) {
        this.error = 'Missing platform or workspace ID';
        this.loading = false;
        return;
      }

      this.checkAuth();
      this.loadWorkspace();
    });
  }

  get platformName(): string {
    return this.platform === 'slack' ? 'Slack' : 'Teams';
  }

  get platformIcon(): string {
    return this.platform === 'slack'
      ? 'https://a.slack-edge.com/80588/marketing/img/icons/icon_slack_hash_colored.png'
      : 'https://upload.wikimedia.org/wikipedia/commons/c/c9/Microsoft_Office_Teams_%282018%E2%80%93present%29.svg';
  }

  checkAuth() {
    const credentials = localStorage.getItem('userCredentials');
    const user = localStorage.getItem('currentUser');

    if (credentials && user) {
      this.isLoggedIn = true;
      const userData = JSON.parse(user);
      this.userEmail = userData.email || userData.Email;
      this.orgName = userData.organizationName || userData.OrganizationName || '';
      this.hasOrg = !!(userData.organizationId || userData.OrganizationId || localStorage.getItem('currentOrgId'));
    }
  }

  loadWorkspace() {
    this.http.get<any>(`${environment.apiUrl}/workspace/${this.platform}/${this.workspaceId}`)
      .subscribe({
        next: (response) => {
          if (response.success) {
            this.workspace = response.workspace;
          } else {
            this.error = response.error || 'Failed to load workspace';
          }
          this.loading = false;
        },
        error: (err) => {
          this.error = err.error?.error || 'Failed to load workspace info';
          this.loading = false;
        }
      });
  }

  goToLogin() {
    const returnUrl = `/connect?platform=${this.platform}&id=${this.workspaceId}`;
    localStorage.setItem('returnUrl', returnUrl);
    this.router.navigate(['/login']);
  }

  connect() {
    if (!this.isLoggedIn || this.connecting) return;

    this.connecting = true;
    const credentials = localStorage.getItem('userCredentials');

    let headers = new HttpHeaders();

    if (credentials) {
      // Decode base64 credentials
      const decoded = atob(credentials);
      const [email, password] = decoded.split(':');
      headers = headers.set('X-User-Email', email);
      headers = headers.set('X-User-Password', password);
    }

    this.http.post<any>(`${environment.apiUrl}/connect-workspace`, {
      platform: this.platform,
      workspaceId: this.workspaceId
    }, { headers }).subscribe({
      next: (response) => {
        if (response.success) {
          this.success = true;
        } else {
          this.error = response.error || 'Failed to connect workspace';
        }
        this.connecting = false;
      },
      error: (err) => {
        this.error = err.error?.error || 'Failed to connect workspace';
        this.connecting = false;
      }
    });
  }

  disconnect() {
    if (!confirm('Are you sure you want to disconnect this workspace? It will revert to the free tier.')) {
      return;
    }

    const credentials = localStorage.getItem('userCredentials');

    let headers = new HttpHeaders();

    if (credentials) {
      const decoded = atob(credentials);
      const [email, password] = decoded.split(':');
      headers = headers.set('X-User-Email', email);
      headers = headers.set('X-User-Password', password);
    }

    this.http.post<any>(`${environment.apiUrl}/disconnect-workspace`, {
      platform: this.platform,
      workspaceId: this.workspaceId
    }, { headers }).subscribe({
      next: () => {
        window.location.reload();
      },
      error: (err) => {
        this.error = err.error?.error || 'Failed to disconnect workspace';
      }
    });
  }

  logout() {
    localStorage.removeItem('userCredentials');
    localStorage.removeItem('currentUser');
    localStorage.removeItem('currentOrgId');
    this.isLoggedIn = false;
    this.userEmail = '';
    this.orgName = '';
    this.hasOrg = false;
  }
}
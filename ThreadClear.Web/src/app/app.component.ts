import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './services/auth.service';
import { OrganizationService } from './services/organization.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {
  title = 'ThreadClear';
  isLoggedIn = false;
  isAdmin = false;
  isOrgAdmin = false;
  userEmail = '';
  userName = '';
  displayName = '';
  fullName = '';
  currentOrgName = '';
  hasOrganization = false;

  constructor(
    private authService: AuthService,
    private orgService: OrganizationService,
    private router: Router
  ) { }

  ngOnInit(): void {
    // Subscribe to auth state
    this.authService.currentUser$.subscribe(user => {
      console.log('Current user:', user);
      if (user) {
        this.isLoggedIn = true;
        this.isAdmin = user.role === 'admin';
        this.userEmail = user.email || '';
        this.userName = user.email?.split('@')[0] || '';
        this.displayName = user.displayName || '';
        this.isOrgAdmin = user.role === 'admin';// || user.permissions?.isOrgAdmin || false;
        console.log('isAdmin set to:', this.isAdmin);
      } else {
        this.isLoggedIn = false;
        // Don't reset other properties - keep last known values
      }
    });

    // Subscribe to current org
    this.orgService.currentOrg$.subscribe(org => {
      console.log('Current organization:', org);
      this.currentOrgName = org?.name || '';
      this.hasOrganization = !!org;
    });
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  isAuthPage(): boolean {
    const path = this.router.url;
    return path === '/login' || path.startsWith('/register');
  }
}
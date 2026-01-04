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
  userEmail = '';
  currentOrgName = '';

  constructor(
    private authService: AuthService,
    private orgService: OrganizationService,
    private router: Router
  ) { }

  ngOnInit(): void {
    // Subscribe to auth state
    this.authService.currentUser$.subscribe(user => {
      this.isLoggedIn = !!user;
      this.isAdmin = user?.role === 'admin' || user?.role === 'admin';
      this.userEmail = user?.email || user?.email || '';
    });

    // Subscribe to current org
    this.orgService.currentOrg$.subscribe(org => {
      this.currentOrgName = org?.name || '';
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

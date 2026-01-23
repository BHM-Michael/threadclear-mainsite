import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { BehaviorSubject, Observable, tap, retry } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../environments/environment';

export interface User {
    id: string;
    email: string;
    displayName: string;
    role: string;
    isActive: boolean;
    permissions: UserPermissions;
    plan?: string;
}

export interface UserPermissions {
    unansweredQuestions: boolean;
    tensionPoints: boolean;
    misalignments: boolean;
    conversationHealth: boolean;
    suggestedActions: boolean;
}

export interface LoginResponse {
    success: boolean;
    token?: string;
    user?: User;
    error?: string;
}

export interface CreateUserRequest {
    email: string;
    password: string;
    unansweredQuestions: boolean;
    tensionPoints: boolean;
    misalignments: boolean;
    conversationHealth: boolean;
    suggestedActions: boolean;
}

@Injectable({
    providedIn: 'root'
})
export class AuthService {
    private apiUrl = environment.apiUrl;
    private currentUserSubject = new BehaviorSubject<User | null>(null);
    public currentUser$ = this.currentUserSubject.asObservable();

    private idleTimeout: any;
    private readonly IDLE_TIME = 30 * 60 * 1000; // 30 minutes
    private readonly SESSION_KEY = 'sessionExpiry';

    constructor(
        private http: HttpClient,
        private router: Router  // ADD THIS
    ) {
        this.restoreSession();
    }

    private restoreSession(): void {
        const storedUser = localStorage.getItem('currentUser');
        const sessionExpiry = localStorage.getItem(this.SESSION_KEY);

        if (storedUser && sessionExpiry) {
            const expiryTime = parseInt(sessionExpiry, 10);

            if (Date.now() < expiryTime) {
                // Session still valid
                this.currentUserSubject.next(JSON.parse(storedUser));
                this.startIdleTimer();
            } else {
                // Session expired - clear and redirect

                this.clearSession();
                // Redirect after a tick to ensure router is ready
                setTimeout(() => {
                    this.router.navigate(['/login']);
                }, 0);
            }
        }
    }

    private clearSession(): void {
        localStorage.removeItem('currentUser');
        localStorage.removeItem('userCredentials');
        localStorage.removeItem('currentOrgId');
        localStorage.removeItem(this.SESSION_KEY);
        this.currentUserSubject.next(null);
    }

    private updateSessionExpiry(): void {
        const newExpiry = Date.now() + this.IDLE_TIME;
        localStorage.setItem(this.SESSION_KEY, newExpiry.toString());
    }

    get currentUser(): User | null {
        return this.currentUserSubject.value;
    }

    get isLoggedIn(): boolean {
        return this.currentUser !== null;
    }

    get isAdmin(): boolean {
        const user = this.currentUser as any;
        return user?.role === 'admin' || user?.Role === 'admin';
    }

    login(email: string, password: string): Observable<any> {
        return this.http.post<any>(`${this.apiUrl}/auth/login`, { email, password })
            .pipe(
                retry({ count: 2, delay: 1000 }),
                tap((response: any) => {
                    const success = response.success || response.Success;
                    const user = response.user || response.User;


                    if (success && user) {
                        // Normalize the permissions property casing
                        if (user.Permissions) {
                            user.permissions = user.Permissions;
                        }
                        if (user.DisplayName) {
                            user.displayName = user.DisplayName;
                        }
                        if (user.Role) {
                            user.role = user.Role;
                        }

                        localStorage.setItem('currentUser', JSON.stringify(user));
                        localStorage.setItem('userCredentials', btoa(`${email}:${password}`));

                        this.updateSessionExpiry();  // ADD THIS
                        this.currentUserSubject.next(user);
                        this.startIdleTimer();  // ADD THIS
                    }
                })
            );
    }

    logout(): void {
        clearTimeout(this.idleTimeout);
        this.clearSession();
        this.router.navigate(['/login']);  // ADD REDIRECT
    }

    updateCurrentUser(user: User): void {
        localStorage.setItem('currentUser', JSON.stringify(user));
        this.currentUserSubject.next(user);
    }

    // Admin functions - add these after updateCurrentUser()

    private getAdminHeaders(): HttpHeaders {
        const credentials = localStorage.getItem('userCredentials');
        if (!credentials) return new HttpHeaders();

        const [email, password] = atob(credentials).split(':');
        return new HttpHeaders({
            'X-Admin-Email': email,
            'X-Admin-Password': password
        });
    }

    getUsers(): Observable<{ success: boolean; users: User[] }> {
        return this.http.get<{ success: boolean; users: User[] }>(
            `${this.apiUrl}/manage/users`,
            { headers: this.getAdminHeaders() }
        );
    }

    createUser(request: CreateUserRequest): Observable<{ success: boolean; user: User }> {
        return this.http.post<{ success: boolean; user: User }>(
            `${this.apiUrl}/manage/users`,
            request,
            { headers: this.getAdminHeaders() }
        );
    }

    updateUserPermissions(userId: string, permissions: UserPermissions): Observable<{ success: boolean }> {
        return this.http.put<{ success: boolean }>(
            `${this.apiUrl}/manage/permissions/${userId}`,
            permissions,
            { headers: this.getAdminHeaders() }
        );
    }

    deleteUser(userId: string): Observable<{ success: boolean }> {
        return this.http.delete<{ success: boolean }>(
            `${this.apiUrl}/manage/user/${userId}`,
            { headers: this.getAdminHeaders() }
        );
    }

    startIdleTimer(): void {
        this.resetIdleTimer();

        // Listen for user activity
        ['mousedown', 'mousemove', 'keypress', 'scroll', 'touchstart'].forEach(event => {
            document.addEventListener(event, () => this.resetIdleTimer(), { passive: true });
        });
    }

    private resetIdleTimer(): void {
        if (this.idleTimeout) {
            clearTimeout(this.idleTimeout);
        }

        if (this.currentUserSubject.value) {
            this.updateSessionExpiry();  // ADD THIS - extend session on activity

            this.idleTimeout = setTimeout(() => {

                this.logout();
            }, this.IDLE_TIME);
        }
    }
}
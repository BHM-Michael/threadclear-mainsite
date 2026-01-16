import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { BehaviorSubject, Observable, tap, retry } from 'rxjs';
import { environment } from '../../environments/environment';

export interface User {
    id: string;
    email: string;
    displayName: string;
    role: string;
    isActive: boolean;
    permissions: UserPermissions;
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

    constructor(private http: HttpClient) {
        // Check for stored user on startup
        const storedUser = localStorage.getItem('currentUser');
        if (storedUser) {
            this.currentUserSubject.next(JSON.parse(storedUser));
        }
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
                retry({ count: 2, delay: 1000 }), // Retry up to 2 times with 1 second delay
                tap((response: any) => {
                    const success = response.success || response.Success;
                    const user = response.user || response.User;
                    console.log('Login response:', response);

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
                        this.currentUserSubject.next(user);
                    }
                })
            );
    }
    logout(): void {
        localStorage.removeItem('currentUser');
        localStorage.removeItem('userCredentials');
        this.currentUserSubject.next(null);
    }

    // Admin functions
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

    getFeaturePricing(): Observable<{ success: boolean; pricing: any[] }> {
        return this.http.get<{ success: boolean; pricing: any[] }>(
            `${this.apiUrl}/manage/pricing`
        );
    }

    updateFeaturePricing(featureName: string, price: number): Observable<{ success: boolean }> {
        return this.http.put<{ success: boolean }>(
            `${this.apiUrl}/manage/pricing/${featureName}`,
            { price },
            { headers: this.getAdminHeaders() }
        );
    }
}
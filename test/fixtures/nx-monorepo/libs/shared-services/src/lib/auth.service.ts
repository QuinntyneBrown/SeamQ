import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { UserModel } from '@myorg/shared-models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private currentUser$ = new BehaviorSubject<UserModel | null>(null);

  getCurrentUser(): Observable<UserModel | null> {
    return this.currentUser$.asObservable();
  }

  login(email: string, password: string): Observable<UserModel> {
    throw new Error('Not implemented');
  }

  logout(): void {
    this.currentUser$.next(null);
  }
}

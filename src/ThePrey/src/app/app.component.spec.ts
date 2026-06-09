import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '@auth0/auth0-angular';
import { of } from 'rxjs';
import { AppComponent } from './app.component';
import { UserStateService } from './users/user-state.service';

describe('AppComponent', () => {
  it('should create the app', async () => {
    const auth = jasmine.createSpyObj<AuthService>(
      'AuthService',
      ['handleRedirectCallback'],
      {
        isLoading$: of(false),
        isAuthenticated$: of(false),
        idTokenClaims$: of(null),
        user$: of(null),
      },
    );
    const userState = jasmine.createSpyObj<UserStateService>('UserStateService', ['init', 'isSyncing']);

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: UserStateService, useValue: userState },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });
});

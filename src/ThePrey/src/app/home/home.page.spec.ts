import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { AuthService } from '@auth0/auth0-angular';
import { provideTranslateService } from '@ngx-translate/core';

import { HomePage } from './home.page';
import { UserStateService } from '../users/user-state.service';

describe('HomePage', () => {
  let component: HomePage;
  let fixture: ComponentFixture<HomePage>;

  beforeEach(async () => {
    const userState = jasmine.createSpyObj<UserStateService>(
      'UserStateService',
      ['init', 'clear'],
      {
        // profile() and syncFailed() are signals — stub them as callable accessors.
        profile: (() => null) as unknown as UserStateService['profile'],
        syncFailed: (() => false) as unknown as UserStateService['syncFailed'],
      },
    );

    await TestBed.configureTestingModule({
      imports: [HomePage],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        provideTranslateService(),
        { provide: AuthService, useValue: jasmine.createSpyObj<AuthService>('AuthService', ['logout']) },
        { provide: UserStateService, useValue: userState },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(HomePage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

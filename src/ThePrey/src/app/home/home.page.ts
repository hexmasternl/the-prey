import {
  Component,
  computed,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import {
  IonButton,
  IonContent,
  IonRefresher,
  IonRefresherContent,
  IonSpinner,
  RefresherCustomEvent,
} from '@ionic/angular/standalone';
import { App } from '@capacitor/app';
import { Browser } from '@capacitor/browser';
import { Capacitor } from '@capacitor/core';
import { Geolocation, WatchPositionCallback } from '@capacitor/geolocation';
import { AuthService, IdToken } from '@auth0/auth0-angular';
import { TranslatePipe } from '@ngx-translate/core';
import { filter, take } from 'rxjs/operators';
import { getCallbackUri } from '../auth.utils';
import { UserStateService } from '../users/user-state.service';
import { GameDto, GamesService } from '../games/games.service';

@Component({
  selector: 'app-home',
  templateUrl: 'home.page.html',
  styleUrls: ['home.page.scss'],
  imports: [
    IonButton,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonSpinner,
    TranslatePipe,
  ],
})
export class HomePage implements OnInit, OnDestroy {
  /** Last known position, formatted to 4 decimals; null until the first GPS fix. */
  lat: string | null = null;
  lon: string | null = null;

  readonly activeGame = signal<GameDto | null>(null);
  readonly activeGameId = computed(() => this.activeGame()?.id ?? null);

  /** False until the first /games/active request resolves; gates the Play Now button. */
  readonly activeGameLoaded = signal(false);

  readonly callsignChars = computed(() =>
    (this.userState.profile()?.callsign ?? '')
      .toUpperCase()
      .split('')
      .map((c) => (c === ' ' ? ' ' : c)),
  );

  readonly callsignAnimDuration = computed(() => {
    const n = this.callsignChars().length || 1;
    return `${n * 280 + 700}ms`;
  });

  private watchId: string | undefined;
  private readonly gamesService = inject(GamesService);

  constructor(
    readonly authService: AuthService,
    readonly userState: UserStateService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    // User sync is handled globally in AppComponent; by the time this page
    // renders the profile is already available.
    this.checkActiveGame();
    this.startLocationWatch();
  }

  ngOnDestroy(): void {
    if (this.watchId) {
      Geolocation.clearWatch({ id: this.watchId });
    }
  }

  private async startLocationWatch(): Promise<void> {
    try {
      const permission = await Geolocation.requestPermissions();
      if (permission.location !== 'granted') return;

      this.watchId = await Geolocation.watchPosition(
        { enableHighAccuracy: false, timeout: 10000 },
        this.onPosition,
      );
    } catch {
      // Permission denied or unavailable — coords stay at fallback
    }
  }

  private readonly onPosition: WatchPositionCallback = (position, err) => {
    if (err || !position) return;
    this.lat = position.coords.latitude.toFixed(4);
    this.lon = position.coords.longitude.toFixed(4);
  };

  private async checkActiveGame(): Promise<void> {
    try {
      const active = await this.gamesService.getActiveGame();
      this.activeGame.set(active);
    } finally {
      this.activeGameLoaded.set(true);
    }
  }

  /** Pull-to-refresh handler: re-fetch the active game, then dismiss the refresher. */
  async handleRefresh(event: RefresherCustomEvent): Promise<void> {
    try {
      await this.checkActiveGame();
    } finally {
      await event.target.complete();
    }
  }

  goToActiveGame(): void {
    const game = this.activeGame();
    if (!game) return;
    const userId = this.userState.profile()?.userId;
    const isHunter = game.hunterUserId === userId;
    this.router.navigate(['/games', game.id, isHunter ? 'hunt' : 'play']);
  }

  goToPlay(): void {
    this.router.navigate(['/games/create']);
  }

  goToPlayfields(): void {
    this.router.navigate(['/playfields']);
  }

  goToSettings(): void {
    this.router.navigate(['/settings']);
  }

  async logout(): Promise<void> {
    await this.userState.clear();
    this.authService.logout({
      logoutParams: { returnTo: getCallbackUri() },
      ...(Capacitor.isNativePlatform() && {
        async openUrl(url: string) {
          await Browser.open({ url, windowName: '_self' });
        },
      }),
    });
  }

  retrySync(): void {
    this.authService.idTokenClaims$
      .pipe(
        take(1),
        filter((claims): claims is IdToken => claims != null),
      )
      .subscribe((claims) => {
        this.userState.init(claims);
      });
  }

  quit(): void {
    App.exitApp();
  }
}

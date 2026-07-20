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
import { environment } from '../../environments/environment';
import { getCallbackUri } from '../auth.utils';
import { UserStateService } from '../users/user-state.service';
import { GameStatusDto, GamesService } from '../games/games.service';
import { openPlayStore } from '../shared/app-update.util';

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

  /**
   * True when the OS location permission was refused. The game is unplayable without
   * GPS, so this blocks Play Now / Resume Game and surfaces a banner. Cleared again
   * once a position fix arrives (the user granted permission after all).
   */
  readonly locationDenied = signal(false);

  /**
   * App version shown in the hero tag. Seeded with the build-time fallback, then
   * replaced at runtime by the real native versionName (the GitVersion semVer baked
   * into the bundle) when running on a native platform.
   */
  readonly appVersion = signal<string>(environment.version);

  readonly activeGame = signal<GameStatusDto | null>(null);
  readonly activeGameId = computed(() => this.activeGame()?.gameId ?? null);

  /** False until the first /games/active request resolves; gates the Play Now button. */
  readonly activeGameLoaded = signal(false);

  /** False until the version check resolves; the menu stays disabled until then. */
  readonly versionChecked = signal(false);

  /** True only when the server returned 409 — the app must be updated to continue. */
  readonly updateRequired = signal(false);

  /**
   * Menu actions are disabled by default: while the version check is in flight
   * (`!versionChecked()`) and whenever an update is required. Any non-409 outcome resolves the
   * check and clears the block, so a backend hiccup only briefly delays the buttons.
   */
  readonly versionBlocked = computed(() => !this.versionChecked() || this.updateRequired());

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
    this.startLocationWatch();
    // Resolve the real native version first, then gate the menu on the version check.
    void this.loadAppVersion().then(() => this.runVersionCheck());
  }

  /**
   * Ionic keeps this page alive in the nav stack, so ngOnInit only ever runs once.
   * Re-check on every entry — the active game can have started, been joined or ended
   * while the user was on another page, and the menu button must reflect that.
   */
  ionViewWillEnter(): void {
    void this.checkActiveGame();
  }

  /**
   * Post the local version to the server gate. The menu is disabled until this resolves;
   * only a 409 keeps it disabled (updateRequired), every other outcome enables it (fail-open).
   */
  private async runVersionCheck(): Promise<void> {
    try {
      const result = await this.gamesService.checkAppVersion(this.appVersion());
      this.updateRequired.set(result === 'update-required');
    } finally {
      this.versionChecked.set(true);
    }
  }

  /** Open the Play Store listing from the update-required banner. */
  openStore(): Promise<void> {
    return openPlayStore();
  }

  /**
   * Read the real app version from the native bundle. App.getInfo() is only
   * implemented on iOS/Android; on the web it throws, so we keep the fallback.
   */
  private async loadAppVersion(): Promise<void> {
    if (!Capacitor.isNativePlatform()) {
      return;
    }
    try {
      const info = await App.getInfo();
      if (info.version) {
        this.appVersion.set(info.version);
      }
    } catch {
      // Version info unavailable — keep the build-time fallback.
    }
  }

  ngOnDestroy(): void {
    if (this.watchId) {
      Geolocation.clearWatch({ id: this.watchId });
    }
  }

  private async startLocationWatch(): Promise<void> {
    try {
      const permission = await Geolocation.requestPermissions();
      if (permission.location === 'denied') {
        this.locationDenied.set(true);
        return;
      }
    } catch {
      // Permissions API unavailable (e.g. web) — let watchPosition surface a denial.
    }

    try {
      this.watchId = await Geolocation.watchPosition(
        { enableHighAccuracy: false, timeout: 10000 },
        this.onPosition,
      );
    } catch {
      // Location unavailable — coords stay at fallback
    }
  }

  private readonly onPosition: WatchPositionCallback = (position, err) => {
    if (err) {
      // Web reports a refused permission as GeolocationPositionError code 1.
      if ((err as { code?: number }).code === 1) {
        this.locationDenied.set(true);
      }
      return;
    }
    if (!position) return;
    this.locationDenied.set(false);
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
    this.router.navigate(['/games', game.gameId, isHunter ? 'hunt' : 'play']);
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

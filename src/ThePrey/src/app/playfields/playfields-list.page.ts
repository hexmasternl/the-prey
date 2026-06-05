import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AlertController,
  IonBadge,
  IonButton,
  IonButtons,
  IonContent,
  IonFab,
  IonFabButton,
  IonHeader,
  IonIcon,
  IonItem,
  IonItemOption,
  IonItemOptions,
  IonItemSliding,
  IonLabel,
  IonList,
  IonSearchbar,
  IonSegment,
  IonSegmentButton,
  IonSpinner,
  IonTitle,
  IonToolbar,
  ViewWillEnter,
} from '@ionic/angular/standalone';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { addIcons } from 'ionicons';
import { add } from 'ionicons/icons';
import { from, Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, filter, switchMap, tap } from 'rxjs/operators';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { PlayFieldRecord, PlayFieldSummaryDto } from './playfield.model';
import { PlayfieldsService } from './playfields.service';

type Tab = 'private' | 'public';

@Component({
  selector: 'app-playfields-list',
  templateUrl: 'playfields-list.page.html',
  styleUrls: ['playfields-list.page.scss'],
  imports: [
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonButton,
    IonContent,
    IonFab,
    IonFabButton,
    IonIcon,
    IonSegment,
    IonSegmentButton,
    IonList,
    IonItemSliding,
    IonItem,
    IonItemOptions,
    IonItemOption,
    IonLabel,
    IonBadge,
    IonSearchbar,
    IonSpinner,
    TranslatePipe,
  ],
})
export class PlayfieldsListPage implements ViewWillEnter {
  private readonly router = inject(Router);
  private readonly playfieldsService = inject(PlayfieldsService);
  private readonly alertCtrl = inject(AlertController);
  private readonly translate = inject(TranslateService);

  readonly activeTab = signal<Tab>('private');
  readonly playfields = signal<PlayFieldRecord[]>([]);
  readonly isSyncing = signal(false);
  readonly syncFailed = signal(false);

  readonly searchQuery$ = new Subject<string>();
  readonly publicResults = signal<PlayFieldSummaryDto[]>([]);
  readonly isSearchingPublic = signal(false);
  readonly currentPublicQuery = signal('');

  constructor() {
    addIcons({ add });

    this.searchQuery$.pipe(
      debounceTime(400),
      distinctUntilChanged(),
      filter(v => v.length >= 3),
      tap(() => this.isSearchingPublic.set(true)),
      switchMap(q => from(this.playfieldsService.searchPublicPlayfields(q))),
      takeUntilDestroyed(),
    ).subscribe({
      next: results => {
        this.publicResults.set(results);
        this.isSearchingPublic.set(false);
      },
      error: () => {
        this.isSearchingPublic.set(false);
      },
    });
  }

  onPublicSearch(value: string): void {
    this.currentPublicQuery.set(value);
    this.searchQuery$.next(value);
  }

  ionViewWillEnter(): void {
    this.loadLocalThenSync();
  }

  private async loadLocalThenSync(): Promise<void> {
    const local = await this.playfieldsService.getLocalPlayfields();
    this.playfields.set(local);
    await this.sync();
  }

  async sync(): Promise<void> {
    this.isSyncing.set(true);
    this.syncFailed.set(false);
    try {
      const records = await this.playfieldsService.syncPlayfields();
      this.playfields.set(records);
    } catch {
      this.syncFailed.set(true);
    } finally {
      this.isSyncing.set(false);
    }
  }

  selectTab(tab: Tab): void {
    this.activeTab.set(tab);
  }

  openDetail(id: string): void {
    this.router.navigate(['/playfields', id]);
  }

  async deletePlayfield(sliding: IonItemSliding, id: string): Promise<void> {
    await sliding.close();

    const [header, message, cancel, confirm] = await Promise.all([
      this.translate.get('PLAYFIELD_LIST.DELETE_CONFIRM_HEADER').toPromise(),
      this.translate.get('PLAYFIELD_LIST.DELETE_CONFIRM_MESSAGE').toPromise(),
      this.translate.get('PLAYFIELD_LIST.DELETE_CONFIRM_CANCEL').toPromise(),
      this.translate.get('PLAYFIELD_LIST.DELETE_CONFIRM_OK').toPromise(),
    ]);

    const alert = await this.alertCtrl.create({
      header,
      message,
      buttons: [
        { text: cancel, role: 'cancel' },
        {
          text: confirm,
          role: 'destructive',
          handler: async () => {
            await this.playfieldsService.delete(id);
            this.playfields.update((list) => list.filter((p) => p.id !== id));
          },
        },
      ],
    });
    await alert.present();
  }

  createPlayfield(): void {
    this.router.navigate(['/playfields/new']);
  }

  back(): void {
    this.router.navigate(['/home']);
  }
}

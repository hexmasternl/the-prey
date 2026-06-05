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
  IonSegment,
  IonSegmentButton,
  IonSpinner,
  IonTitle,
  IonToolbar,
  ViewWillEnter,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { add } from 'ionicons/icons';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { PlayFieldRecord } from './playfield.model';
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
    IonSpinner,
    TranslatePipe,
  ],
})
export class PlayfieldsListPage implements ViewWillEnter {
  private readonly router = inject(Router);
  private readonly playfieldsService = inject(PlayfieldsService);
  private readonly alertCtrl = inject(AlertController);
  private readonly translate = inject(TranslateService);

  constructor() {
    addIcons({ add });
  }

  readonly activeTab = signal<Tab>('private');
  readonly playfields = signal<PlayFieldRecord[]>([]);
  readonly isSyncing = signal(false);
  readonly syncFailed = signal(false);

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

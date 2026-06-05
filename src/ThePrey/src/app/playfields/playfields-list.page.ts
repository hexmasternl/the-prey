import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
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
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { add } from 'ionicons/icons';
import { TranslatePipe } from '@ngx-translate/core';
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
export class PlayfieldsListPage implements OnInit {
  private readonly router = inject(Router);
  private readonly playfieldsService = inject(PlayfieldsService);

  constructor() {
    addIcons({ add });
  }

  readonly activeTab = signal<Tab>('private');
  readonly playfields = signal<PlayFieldRecord[]>([]);
  readonly isSyncing = signal(false);
  readonly syncFailed = signal(false);

  async ngOnInit(): Promise<void> {
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
    await this.playfieldsService.deleteLocal(id);
    this.playfields.update((list) => list.filter((p) => p.id !== id));
  }

  createPlayfield(): void {
    this.router.navigate(['/playfields/new']);
  }

  back(): void {
    this.router.navigate(['/home']);
  }
}

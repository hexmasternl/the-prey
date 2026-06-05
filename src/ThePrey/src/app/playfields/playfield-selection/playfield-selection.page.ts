import { Component, inject, signal } from '@angular/core';
import {
  IonBadge,
  IonButton,
  IonButtons,
  IonContent,
  IonFooter,
  IonHeader,
  IonItem,
  IonLabel,
  IonList,
  IonSearchbar,
  IonSpinner,
  IonTitle,
  IonToolbar,
  ModalController,
  ViewWillEnter,
} from '@ionic/angular/standalone';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { from, Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, filter, switchMap, tap } from 'rxjs/operators';
import { TranslatePipe } from '@ngx-translate/core';
import { PlayFieldRecord } from '../playfield.model';
import { PlayfieldsService } from '../playfields.service';
import { UserStateService } from '../../users/user-state.service';

@Component({
  selector: 'app-playfield-selection',
  templateUrl: 'playfield-selection.page.html',
  styleUrls: ['playfield-selection.page.scss'],
  imports: [
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonButton,
    IonContent,
    IonFooter,
    IonList,
    IonItem,
    IonLabel,
    IonBadge,
    IonSearchbar,
    IonSpinner,
    TranslatePipe,
  ],
})
export class PlayfieldSelectionPage implements ViewWillEnter {
  private readonly playfieldsService = inject(PlayfieldsService);
  private readonly userState = inject(UserStateService);
  private readonly modalCtrl = inject(ModalController);

  readonly displayList = signal<PlayFieldRecord[]>([]);
  readonly selectedPlayfield = signal<PlayFieldRecord | null>(null);
  readonly isSearching = signal(false);
  readonly searchQuery = signal('');

  private readonly searchQuery$ = new Subject<string>();

  constructor() {
    this.searchQuery$.pipe(
      debounceTime(400),
      distinctUntilChanged(),
      filter(v => v.length >= 3),
      tap(() => this.isSearching.set(true)),
      switchMap(q => from(this.playfieldsService.searchPublicPlayfields(q))),
      takeUntilDestroyed(),
    ).subscribe({
      next: results => {
        this.displayList.set(results as PlayFieldRecord[]);
        this.isSearching.set(false);
      },
      error: () => {
        this.isSearching.set(false);
        this.loadLocal();
      },
    });
  }

  ionViewWillEnter(): void {
    this.loadLocal();
  }

  private async loadLocal(): Promise<void> {
    const local = await this.playfieldsService.getLocalPlayfields();
    this.displayList.set(local);
  }

  onSearch(value: string): void {
    this.searchQuery.set(value);
    if (value.length < 3) {
      this.loadLocal();
    }
    this.searchQuery$.next(value);
  }

  toggleSelect(record: PlayFieldRecord): void {
    this.selectedPlayfield.update(prev => prev?.id === record.id ? null : record);
  }

  isSelected(record: PlayFieldRecord): boolean {
    return this.selectedPlayfield()?.id === record.id;
  }

  isOwned(record: PlayFieldRecord): boolean {
    return record.ownerId === this.userState.profile()?.userId;
  }

  async confirm(): Promise<void> {
    await this.modalCtrl.dismiss({ playfield: this.selectedPlayfield() });
  }

  async cancel(): Promise<void> {
    await this.modalCtrl.dismiss(null);
  }
}

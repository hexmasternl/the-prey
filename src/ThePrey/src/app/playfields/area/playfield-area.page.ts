import {
  Component,
  ElementRef,
  OnDestroy,
  ViewChild,
  inject,
  signal,
} from '@angular/core';
import { Location } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonFooter,
  IonHeader,
  IonSpinner,
  IonTitle,
  IonToolbar,
  ToastController,
  ViewDidEnter,
} from '@ionic/angular/standalone';
import { TranslatePipe } from '@ngx-translate/core';
import { Geolocation } from '@capacitor/geolocation';
import * as L from 'leaflet';
import { GpsCoordinateDto } from '../playfield.model';
import { PlayfieldsService } from '../playfields.service';
import { PlayfieldDraftService } from '../playfield-draft.service';

@Component({
  selector: 'app-playfield-area',
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-title>{{ 'PLAYFIELD_AREA.TITLE' | translate }}</ion-title>
      </ion-toolbar>
    </ion-header>

    @if (isLoading()) {
      <div class="loading-overlay">
        <ion-spinner name="crescent" />
      </div>
    }

    <ion-content [scrollY]="false">
      <div #mapContainer class="map-container"></div>
    </ion-content>

    <ion-footer>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-button (click)="onReset()">{{ 'PLAYFIELD_AREA.RESET' | translate }}</ion-button>
          <ion-button (click)="onCancel()">{{ 'PLAYFIELD_AREA.CANCEL' | translate }}</ion-button>
        </ion-buttons>
        <ion-buttons slot="end">
          <ion-button [disabled]="pointCount() < 3" (click)="onSave()">
            {{ 'PLAYFIELD_AREA.SAVE' | translate }}
          </ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-footer>
  `,
  styles: [
    `
      .map-container {
        position: absolute;
        top: 0;
        left: 0;
        right: 0;
        bottom: 0;
      }
      .loading-overlay {
        position: fixed;
        inset: 0;
        display: flex;
        align-items: center;
        justify-content: center;
        background: rgba(0, 0, 0, 0.4);
        z-index: 9999;
      }
    `,
  ],
  imports: [
    IonHeader,
    IonToolbar,
    IonTitle,
    IonContent,
    IonFooter,
    IonButton,
    IonButtons,
    IonSpinner,
    TranslatePipe,
  ],
})
export class PlayfieldAreaPage implements ViewDidEnter, OnDestroy {
  @ViewChild('mapContainer', { static: false }) mapContainer!: ElementRef<HTMLDivElement>;

  private readonly route = inject(ActivatedRoute);
  private readonly location = inject(Location);
  private readonly playfieldsService = inject(PlayfieldsService);
  private readonly draftService = inject(PlayfieldDraftService);
  private readonly toastController = inject(ToastController);

  readonly isLoading = signal(false);
  readonly pointCount = signal(0);

  private readonly points: GpsCoordinateDto[] = [];
  private readonly markers: L.CircleMarker[] = [];
  private map: L.Map | undefined;
  private polygon: L.Polygon | undefined;
  private playFieldId = '';

  ionViewDidEnter(): void {
    this.playFieldId = this.route.snapshot.paramMap.get('id') ?? '';

    if (!this.map) {
      this.map = L.map(this.mapContainer.nativeElement, { zoom: 2, center: [0, 0] });
      if ((this.map as any).tap) (this.map as any).tap.disable();
      L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(this.map);
      this.map.on('click', (e: L.LeafletMouseEvent) => this.onMapClick(e));
    }

    this.loadInitialState();
  }

  ngOnDestroy(): void {
    this.map?.remove();
    this.map = undefined;
  }

  private async loadInitialState(): Promise<void> {
    this.clearPoints();

    if (this.playFieldId === 'new') {
      const draft = this.draftService.points();
      if (draft.length > 0) {
        for (const pt of draft) {
          this.addPoint(L.latLng(pt.latitude, pt.longitude));
        }
        if (this.polygon) {
          this.map!.fitBounds(this.polygon.getBounds(), { padding: [16, 16] });
        }
      } else {
        await this.centreOnDeviceLocation();
      }
      return;
    }

    if (!this.playFieldId) return;

    this.isLoading.set(true);
    try {
      const playfield = await this.playfieldsService.getById(this.playFieldId);

      if (playfield.points.length > 0) {
        for (const pt of playfield.points) {
          this.addPoint(L.latLng(pt.latitude, pt.longitude));
        }
        if (this.polygon) {
          this.map!.fitBounds(this.polygon.getBounds(), { padding: [16, 16] });
        } else {
          this.map!.setView(this.computeCentroid(playfield.points), 15);
        }
      } else {
        await this.centreOnDeviceLocation();
      }
    } catch {
      await this.centreOnDeviceLocation();
    } finally {
      this.isLoading.set(false);
    }
  }

  private async centreOnDeviceLocation(): Promise<void> {
    try {
      const permission = await Geolocation.requestPermissions();
      if (permission.location !== 'granted') {
        this.map!.setView([0, 0], 2);
        return;
      }
      const pos = await Geolocation.getCurrentPosition({ timeout: 8000 });
      this.map!.setView([pos.coords.latitude, pos.coords.longitude], 15);
    } catch {
      this.map!.setView([0, 0], 2);
    }
  }

  private onMapClick(e: L.LeafletMouseEvent): void {
    this.addPoint(e.latlng);
  }

  private addPoint(latlng: L.LatLng): void {
    const point: GpsCoordinateDto = { latitude: latlng.lat, longitude: latlng.lng };
    this.points.push(point);

    const marker = L.circleMarker(latlng, {
      radius: 8,
      color: '#22c55e',
      fillColor: '#22c55e',
      fillOpacity: 1,
    }).addTo(this.map!);

    this.markers.push(marker);

    marker.on('click', (e: L.LeafletMouseEvent) => {
      L.DomEvent.stopPropagation(e);
      const idx = this.markers.indexOf(marker);
      if (idx !== -1) {
        marker.remove();
        this.markers.splice(idx, 1);
        this.points.splice(idx, 1);
        this.pointCount.set(this.points.length);
        this.rebuildPolygon();
      }
    });

    this.pointCount.set(this.points.length);
    this.rebuildPolygon();
  }

  private clearPoints(): void {
    for (const m of this.markers) m.remove();
    this.markers.length = 0;
    this.points.length = 0;
    this.pointCount.set(0);
    if (this.polygon) {
      this.polygon.remove();
      this.polygon = undefined;
    }
  }

  private rebuildPolygon(): void {
    if (this.polygon) {
      this.polygon.remove();
      this.polygon = undefined;
    }
    if (this.points.length >= 3) {
      this.polygon = L.polygon(
        this.points.map((p) => L.latLng(p.latitude, p.longitude)),
        { color: '#22c55e', fillColor: '#22c55e', fillOpacity: 0.25, weight: 2 },
      ).addTo(this.map!);
    }
  }

  private computeCentroid(pts: GpsCoordinateDto[]): L.LatLngExpression {
    const lat = pts.reduce((s, p) => s + p.latitude, 0) / pts.length;
    const lng = pts.reduce((s, p) => s + p.longitude, 0) / pts.length;
    return [lat, lng];
  }

  onReset(): void {
    this.clearPoints();
  }

  async onSave(): Promise<void> {
    if (this.points.length < 3) return;

    if (this.playFieldId === 'new') {
      this.draftService.set(this.points);
      this.location.back();
      return;
    }

    try {
      await this.playfieldsService.updateArea(this.playFieldId, this.points);
      this.location.back();
    } catch (err: any) {
      const message = err?.error?.message ?? err?.message ?? 'Failed to save area.';
      const toast = await this.toastController.create({
        message,
        duration: 3000,
        color: 'danger',
        position: 'bottom',
      });
      await toast.present();
    }
  }

  onCancel(): void {
    this.location.back();
  }
}

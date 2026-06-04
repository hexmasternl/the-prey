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
import { Geolocation } from '@capacitor/geolocation';
import * as L from 'leaflet';
import { GpsCoordinateDto } from '../playfield.model';
import { PlayfieldsService } from '../playfields.service';

@Component({
  selector: 'app-playfield-area',
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-title>Set Area</ion-title>
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
          <ion-button (click)="onCancel()">Cancel</ion-button>
        </ion-buttons>
        <ion-buttons slot="end">
          <ion-button [disabled]="pointCount() < 3" (click)="onSave()">Save</ion-button>
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
  ],
})
export class PlayfieldAreaPage implements ViewDidEnter, OnDestroy {
  @ViewChild('mapContainer', { static: false }) mapContainer!: ElementRef<HTMLDivElement>;

  private readonly route = inject(ActivatedRoute);
  private readonly location = inject(Location);
  private readonly playfieldsService = inject(PlayfieldsService);
  private readonly toastController = inject(ToastController);

  readonly isLoading = signal(false);
  readonly pointCount = signal(0);

  private readonly points: GpsCoordinateDto[] = [];
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
    if (!this.playFieldId) return;

    this.isLoading.set(true);
    try {
      const playfield = await this.playfieldsService.getById(this.playFieldId);

      if (playfield.points.length > 0) {
        for (const pt of playfield.points) {
          this.points.push(pt);
          L.circleMarker(L.latLng(pt.latitude, pt.longitude), {
            radius: 6,
            color: '#22c55e',
            fillColor: '#22c55e',
            fillOpacity: 1,
          }).addTo(this.map!);
        }
        this.pointCount.set(this.points.length);
        this.rebuildPolygon();
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
    const { lat, lng } = e.latlng;
    this.points.push({ latitude: lat, longitude: lng });
    this.pointCount.set(this.points.length);

    L.circleMarker(e.latlng, {
      radius: 6,
      color: '#22c55e',
      fillColor: '#22c55e',
      fillOpacity: 1,
    }).addTo(this.map!);

    this.rebuildPolygon();
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

  async onSave(): Promise<void> {
    if (this.points.length < 3) return;
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

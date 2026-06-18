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
  IonFab,
  IonFabButton,
  IonFooter,
  IonHeader,
  IonIcon,
  IonSpinner,
  IonTitle,
  IonToolbar,
  ViewDidEnter,
} from '@ionic/angular/standalone';
import { TranslatePipe } from '@ngx-translate/core';
import { addIcons } from 'ionicons';
import { trashOutline } from 'ionicons/icons';
import { Geolocation } from '@capacitor/geolocation';
import * as L from 'leaflet';
import { GpsCoordinateDto } from '../playfield.model';
import { PlayfieldsService } from '../playfields.service';
import { PlayfieldDraftService } from '../playfield-draft.service';
import { MAP_COLORS } from '../../shared/map-colors';

addIcons({ trashOutline });

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
        <ion-spinner name="lines" color="primary" />
      </div>
    }

    <ion-content [scrollY]="false">
      <div #mapContainer class="map-container"></div>

      @if (hasSelection()) {
        <ion-fab slot="fixed" vertical="top" horizontal="end">
          <ion-fab-button color="danger" size="small" (click)="removeSelected()">
            <ion-icon name="trash-outline"></ion-icon>
          </ion-fab-button>
        </ion-fab>
      }
    </ion-content>

    <ion-footer>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-button fill="outline" (click)="onReset()">{{ 'PLAYFIELD_AREA.RESET' | translate }}</ion-button>
          <ion-button fill="outline" (click)="onCancel()">{{ 'PLAYFIELD_AREA.CANCEL' | translate }}</ion-button>
        </ion-buttons>
        <ion-buttons slot="end">
          <ion-button
            fill="solid"
            color="primary"
            class="save-action"
            [disabled]="pointCount() < 3"
            (click)="onSave()"
          >
            {{ 'PLAYFIELD_AREA.SAVE' | translate }}
          </ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-footer>
  `,
  styles: [
    `
      :host {
        --ion-toolbar-background: var(--tp-bg-void);
        --ion-toolbar-color: var(--tp-text);
      }
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
        background: var(--tp-scrim);
        z-index: 9999;
      }
      .save-action {
        --border-radius: 3px;
        box-shadow: 0 0 8px var(--tp-signal-glow);
      }
      ion-button[fill='outline'] {
        --border-radius: 3px;
      }
    `,
  ],
  imports: [
    IonHeader,
    IonToolbar,
    IonTitle,
    IonContent,
    IonFab,
    IonFabButton,
    IonIcon,
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

  readonly isLoading = signal(false);
  readonly pointCount = signal(0);
  readonly hasSelection = signal(false);

  private readonly points: GpsCoordinateDto[] = [];
  private readonly markers: L.Marker[] = [];
  private selectedMarker: L.Marker | undefined;
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

    const draft = this.draftService.points();

    if (draft.length > 0) {
      for (const pt of draft) {
        this.addPoint(L.latLng(pt.latitude, pt.longitude));
      }
      if (this.polygon) {
        this.map!.fitBounds(this.polygon.getBounds(), { padding: [16, 16] });
      }
      return;
    }

    if (this.playFieldId === 'new' || !this.playFieldId) {
      await this.centreOnDeviceLocation();
      return;
    }

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
          this.map!.setView(this.computeCentroid(playfield.points), 16);
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
      const pos = await Geolocation.getCurrentPosition({ timeout: 8000, enableHighAccuracy: true });
      this.map!.setView([pos.coords.latitude, pos.coords.longitude], 16);
    } catch {
      this.map!.setView([0, 0], 2);
    }
  }

  private onMapClick(e: L.LeafletMouseEvent): void {
    if (this.selectedMarker) {
      this.selectedMarker.setIcon(this.createMarkerIcon(false));
      this.selectedMarker = undefined;
      this.hasSelection.set(false);
    }
    this.addPoint(e.latlng);
  }

  private createMarkerIcon(selected: boolean): L.DivIcon {
    const border = selected ? MAP_COLORS.HUNTER : MAP_COLORS.SIGNAL;
    return L.divIcon({
      className: '',
      html: `<div style="width:16px;height:16px;border-radius:50%;background:${MAP_COLORS.SIGNAL};border:3px solid ${border};box-sizing:border-box;"></div>`,
      iconSize: [16, 16],
      iconAnchor: [8, 8],
    });
  }

  private addPoint(latlng: L.LatLng): void {
    const point: GpsCoordinateDto = { latitude: latlng.lat, longitude: latlng.lng };
    this.points.push(point);

    const marker = L.marker(latlng, {
      icon: this.createMarkerIcon(false),
      draggable: true,
    }).addTo(this.map!);

    this.markers.push(marker);

    marker.on('click', (e: L.LeafletMouseEvent) => {
      L.DomEvent.stopPropagation(e);
      if (this.selectedMarker === marker) {
        marker.setIcon(this.createMarkerIcon(false));
        this.selectedMarker = undefined;
        this.hasSelection.set(false);
      } else {
        if (this.selectedMarker) {
          this.selectedMarker.setIcon(this.createMarkerIcon(false));
        }
        marker.setIcon(this.createMarkerIcon(true));
        this.selectedMarker = marker;
        this.hasSelection.set(true);
      }
    });

    marker.on('dragend', () => {
      const idx = this.markers.indexOf(marker);
      if (idx !== -1) {
        const pos = marker.getLatLng();
        this.points[idx] = { latitude: pos.lat, longitude: pos.lng };
        this.rebuildPolygon();
      }
      marker.setIcon(this.createMarkerIcon(false));
      if (this.selectedMarker === marker) {
        this.selectedMarker = undefined;
        this.hasSelection.set(false);
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
    this.selectedMarker = undefined;
    this.hasSelection.set(false);
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
        { color: MAP_COLORS.SIGNAL, fillColor: MAP_COLORS.SIGNAL, fillOpacity: 0.25, weight: 2 },
      ).addTo(this.map!);
    }
  }

  private computeCentroid(pts: GpsCoordinateDto[]): L.LatLngExpression {
    const lat = pts.reduce((s, p) => s + p.latitude, 0) / pts.length;
    const lng = pts.reduce((s, p) => s + p.longitude, 0) / pts.length;
    return [lat, lng];
  }

  removeSelected(): void {
    if (!this.selectedMarker) return;
    const idx = this.markers.indexOf(this.selectedMarker);
    if (idx !== -1) {
      this.selectedMarker.remove();
      this.markers.splice(idx, 1);
      this.points.splice(idx, 1);
      this.pointCount.set(this.points.length);
      this.rebuildPolygon();
    }
    this.selectedMarker = undefined;
    this.hasSelection.set(false);
  }

  onReset(): void {
    this.clearPoints();
  }

  onSave(): void {
    if (this.points.length < 3) return;
    this.draftService.set(this.points);
    this.location.back();
  }

  onCancel(): void {
    this.location.back();
  }
}

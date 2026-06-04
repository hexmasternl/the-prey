import {
  AfterViewInit,
  Component,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  ViewChild,
} from '@angular/core';
import * as L from 'leaflet';
import { GpsCoordinateDto } from '../playfield.model';

@Component({
  selector: 'app-playfield-map',
  template: `<div #mapContainer style="width:100%;height:220px;"></div>`,
  styles: [`:host { display: block; }`],
})
export class PlayfieldMapComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input() coordinates: GpsCoordinateDto[] = [];
  @Input() fallbackCenter: { lat: number; lon: number } | null = null;

  @ViewChild('mapContainer', { static: true }) mapContainer!: ElementRef<HTMLDivElement>;

  private map: L.Map | undefined;
  private polygon: L.Polygon | undefined;

  ngAfterViewInit(): void {
    this.initMap();
  }

  ngOnChanges(): void {
    if (this.map) {
      this.renderContent();
    }
  }

  ngOnDestroy(): void {
    this.map?.remove();
    this.map = undefined;
  }

  private initMap(): void {
    this.map = L.map(this.mapContainer.nativeElement, {
      zoomControl: false,
      attributionControl: false,
    });

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(this.map);

    this.disableInteractions();
    this.renderContent();
  }

  private disableInteractions(): void {
    const m = this.map!;
    m.dragging.disable();
    m.touchZoom.disable();
    m.doubleClickZoom.disable();
    m.scrollWheelZoom.disable();
    m.boxZoom.disable();
    m.keyboard.disable();
    if ((m as any).tap) (m as any).tap.disable();
  }

  private renderContent(): void {
    if (this.polygon) {
      this.polygon.remove();
      this.polygon = undefined;
    }

    const pts = this.coordinates;

    if (pts.length >= 3) {
      const latLngs = pts.map((c) => L.latLng(c.latitude, c.longitude));
      this.polygon = L.polygon(latLngs, {
        color: '#3b82f6',
        fillColor: '#3b82f6',
        fillOpacity: 0.25,
        weight: 2,
      }).addTo(this.map!);
      this.map!.fitBounds(this.polygon.getBounds(), { padding: [16, 16] });
    } else if (this.fallbackCenter) {
      this.map!.setView([this.fallbackCenter.lat, this.fallbackCenter.lon], 15);
    } else {
      this.map!.setView([0, 0], 2);
    }
  }
}

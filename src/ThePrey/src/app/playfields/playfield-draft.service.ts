import { Injectable, signal } from '@angular/core';
import { GpsCoordinateDto } from './playfield.model';

@Injectable({ providedIn: 'root' })
export class PlayfieldDraftService {
  readonly points = signal<GpsCoordinateDto[]>([]);

  set(points: GpsCoordinateDto[]): void {
    this.points.set([...points]);
  }

  clear(): void {
    this.points.set([]);
  }
}

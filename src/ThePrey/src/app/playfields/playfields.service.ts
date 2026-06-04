import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { PlayFieldRecord, PlayFieldSummaryDto, UpsertPlayFieldRequest } from './playfield.model';
import { PlayfieldsDbService } from './playfields-db.service';
import { UserStateService } from '../users/user-state.service';

@Injectable({ providedIn: 'root' })
export class PlayfieldsService {
  private readonly http = inject(HttpClient);
  private readonly db = inject(PlayfieldsDbService);
  private readonly userState = inject(UserStateService);

  private get apiBase(): string {
    return `${environment.apiUrl}/playfields`;
  }

  private getMyPlayfields(): Promise<PlayFieldSummaryDto[]> {
    return firstValueFrom(this.http.get<PlayFieldSummaryDto[]>(this.apiBase));
  }

  private upsertOnServer(record: PlayFieldRecord): Promise<PlayFieldSummaryDto> {
    const body: UpsertPlayFieldRequest = {
      name: record.name,
      isPublic: record.isPublic,
      points: record.points,
      lastUpdatedOn: record.lastUpdatedOn,
    };
    return firstValueFrom(
      this.http.put<PlayFieldSummaryDto>(`${this.apiBase}/${record.id}`, body),
    );
  }

  /**
   * Two-phase sync:
   * 1. Download server playfields → merge into IndexedDB (server-wins on lastUpdatedOn).
   * 2. Push local records with isSynced=false to the server via PUT /playfields/{id}.
   */
  async syncPlayfields(): Promise<PlayFieldRecord[]> {
    const ownerId = this.userState.profile()?.userId;
    if (!ownerId) return [];

    // Phase 1 — download and merge
    const serverList = await this.getMyPlayfields();
    for (const dto of serverList) {
      const record: PlayFieldRecord = { ...dto, points: [], isSynced: true };
      await this.db.upsert(record);
    }

    // Phase 2 — upload unsynced local records
    const unsynced = await this.db.getUnsynced();
    for (const record of unsynced) {
      try {
        await this.upsertOnServer(record);
        await this.db.markSynced(record.id);
      } catch {
        // Upload failure is non-fatal — record remains unsynced for the next attempt
      }
    }

    return this.db.getAll(ownerId);
  }

  async deleteLocal(id: string): Promise<void> {
    // TODO: add server-side delete (DELETE /playfields/{id}) in a future change
    await this.db.delete(id);
  }
}

import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export interface ActiveGameDto {
  gameId: string;
}

export interface CreateGameRequest {
  playfieldId: string;
  displayName: string;
  gameDuration: number;
  hunterDelayTime: number;
  finalStageDuration: number;
  defaultLocationInterval: number;
  finalLocationInterval: number;
  enablePreyBoundaryPenalties?: boolean;
  enableHunterBoundaryPenalty?: boolean;
  profilePictureUrl?: string;
}

export interface GameDto {
  id: string;
  gameCode: string;
  playfieldId: string;
  ownerUserId: string;
  status: string;
  lobby: LobbyPlayerDto[];
  hunter: ParticipantDto | null;
  preys: ParticipantDto[];
  startedAt: string | null;
}

export interface LobbyPlayerDto {
  userId: string;
  displayName: string;
  profilePictureUrl: string | null;
}

export interface ParticipantDto {
  userId: string;
  displayName: string;
  role: string;
}

@Injectable({ providedIn: 'root' })
export class GamesService {
  private readonly http = inject(HttpClient);

  private get apiBase(): string {
    return `${environment.apiUrl}/games`;
  }

  getActiveGame(): Promise<ActiveGameDto | null> {
    return firstValueFrom(
      this.http.get<ActiveGameDto>(`${this.apiBase}/active`).pipe(
        catchError(() => of(null))
      )
    );
  }

  createGame(request: CreateGameRequest): Promise<GameDto> {
    return firstValueFrom(
      this.http.post<GameDto>(this.apiBase, request)
    );
  }
}

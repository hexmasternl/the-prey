import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';

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

export interface GameConfigurationDto {
  gameDuration: number;
  hunterDelayTime: number;
  finalStageDuration: number;
  defaultLocationInterval: number;
  finalLocationInterval: number;
  enablePreyBoundaryPenalties: boolean;
  enableHunterBoundaryPenalty: boolean;
}

export interface GameDto {
  id: string;
  gameCode: string;
  playfieldId: string;
  ownerUserId: string;
  status: string;
  configuration: GameConfigurationDto;
  lobby: LobbyPlayerDto[];
  hunter: ParticipantDto | null;
  preys: ParticipantDto[];
  startedAt: string | null;
  designatedHunterUserId: string | null;
}

export interface LobbyPlayerDto {
  userId: string;
  displayName: string;
  profilePictureUrl: string | null;
  isReady: boolean;
  designatedHunter: boolean;
}

export interface ParticipantDto {
  userId: string;
  displayName: string;
  role: string;
}

export interface GpsCoordinateDto {
  latitude: number;
  longitude: number;
}

export interface GameParticipantStatusDto {
  userId: string;
  callsign: string;
  lastKnownLocation: GpsCoordinateDto | null;
  hasActivePenalty: boolean;
  state: string;
}

export interface GameStatusDto {
  gameId: string;
  playfieldName: string;
  playfieldCoordinates: GpsCoordinateDto[];
  hunter: GameParticipantStatusDto | null;
  preys: GameParticipantStatusDto[];
  gameDurationLeft: number;
  nextPingDuration: number;
  isEndgame: boolean;
  preysLeft: number;
}

@Injectable({ providedIn: 'root' })
export class GamesService {
  private readonly http = inject(HttpClient);

  private get apiBase(): string {
    return `${environment.apiUrl}/games`;
  }

  getActiveGame(): Promise<GameDto | null> {
    return firstValueFrom(
      this.http.get<GameDto>(`${this.apiBase}/active`).pipe(
        catchError(() => of(null))
      )
    );
  }

  createGame(request: CreateGameRequest): Promise<GameDto> {
    return firstValueFrom(
      this.http.post<GameDto>(this.apiBase, request)
    );
  }

  getGame(id: string): Promise<GameDto> {
    return firstValueFrom(this.http.get<GameDto>(`${this.apiBase}/${id}`));
  }

  joinGame(gameId: string, displayName: string, profilePictureUrl?: string | null): Promise<GameDto> {
    return firstValueFrom(
      this.http.post<GameDto>(`${this.apiBase}/${gameId}/lobby`, { displayName, profilePictureUrl })
    );
  }

  setHunter(gameId: string, userId: string): Promise<GameDto> {
    return firstValueFrom(
      this.http.post<GameDto>(`${this.apiBase}/${gameId}/hunter`, { newHunterUserId: userId })
    );
  }

  removePlayer(gameId: string, userId: string): Promise<GameDto> {
    return firstValueFrom(
      this.http.delete<GameDto>(`${this.apiBase}/${gameId}/lobby/${userId}`)
    );
  }

  updateSettings(gameId: string, config: GameConfigurationDto): Promise<GameDto> {
    return firstValueFrom(
      this.http.put<GameDto>(`${this.apiBase}/${gameId}/settings`, config)
    );
  }

  updateConfig(gameId: string, config: GameConfigurationDto): Promise<GameDto> {
    return firstValueFrom(
      this.http.put<GameDto>(`${this.apiBase}/${gameId}/config`, config)
    );
  }

  setReady(gameId: string): Promise<GameDto> {
    return firstValueFrom(
      this.http.post<GameDto>(`${this.apiBase}/${gameId}/lobby/ready`, {})
    );
  }

  getGameStatus(gameId: string): Promise<GameStatusDto> {
    return firstValueFrom(this.http.get<GameStatusDto>(`${this.apiBase}/${gameId}/status`));
  }

  tagPlayer(gameId: string, participantId: string): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.apiBase}/${gameId}/participants/${participantId}/tag`, {})
    );
  }

  connectLobbyStream(gameId: string, token: string): EventSource {
    return new EventSource(`${this.apiBase}/${gameId}/lobby/stream?token=${encodeURIComponent(token)}`);
  }
}

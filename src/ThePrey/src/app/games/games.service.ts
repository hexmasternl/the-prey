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
  participants: ParticipantDto[];
  hunterUserId: string | null;
  preys: string[];
  startedAt: string | null;
  createdAt: string;
  endsAt: string | null;
  cleanUpAfter: string;
  outcome: string;
  completedAt: string | null;
  /** True when the requesting user owns this game. Computed server-side per caller. */
  isOwnerPlayer: boolean;
  /** True when every precondition to start the game is met (enough players, hunter set, all ready). */
  isReadyToStart: boolean;
}

export interface ParticipantDto {
  userId: string;
  displayName: string;
  profilePictureUrl: string | null;
  isReady: boolean;
  state: string;
  lastKnownLocation: { latitude: number; longitude: number } | null;
  hasActivePenalty: boolean;
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
  hunterUserId: string | null;
  participants: GameParticipantStatusDto[];
  gameDurationLeft: number;
  nextPingDuration: number;
  isEndgame: boolean;
  preysLeft: number;
}

/** Response to a POST /games/{id}/locations call. Mirrors the backend RecordLocationResponse. */
export interface RecordLocationResponse {
  accepted: boolean;
  nextLocationIntervalSeconds: number;
  penaltyIntervalSeconds: number | null;
  penaltyEndsAt: string | null;
}

/** Role-specific in-game state returned by GET /games/{id}/state. */
export interface GameStateDto {
  hunterDistanceMeters: number | null;
  preyLocations: GpsCoordinateDto[];
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

  joinGame(
    gameId: string,
    joinCode: string,
    displayName: string,
    profilePictureUrl?: string | null,
  ): Promise<GameDto> {
    return firstValueFrom(
      this.http.post<GameDto>(`${this.apiBase}/${gameId}/lobby`, { joinCode, displayName, profilePictureUrl })
    );
  }

  startGame(gameId: string, hunterUserId: string): Promise<GameDto> {
    return firstValueFrom(
      this.http.post<GameDto>(`${this.apiBase}/${gameId}/start`, { hunterUserId })
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

  /** Fetch the role-specific in-game state (hunter distance for preys, prey locations for hunters). */
  getGameState(gameId: string): Promise<GameStateDto> {
    return firstValueFrom(this.http.get<GameStateDto>(`${this.apiBase}/${gameId}/state`));
  }

  /**
   * Report a GPS fix to the backend. The Auth0 Bearer token is attached automatically by
   * `authTokenInterceptor` (every request to the API origin is authenticated). Returns the
   * server-chosen next reporting interval so the caller can schedule its next post.
   */
  recordLocation(
    gameId: string,
    latitude: number,
    longitude: number,
    accuracy: number,
    recordedAt: string,
  ): Promise<RecordLocationResponse> {
    return firstValueFrom(
      this.http.post<RecordLocationResponse>(`${this.apiBase}/${gameId}/locations`, {
        latitude,
        longitude,
        accuracy,
        recordedAt,
      }),
    );
  }

  tagPlayer(gameId: string, participantId: string): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.apiBase}/${gameId}/participants/${participantId}/tag`, {})
    );
  }
}

export interface GpsCoordinateDto {
  latitude: number;
  longitude: number;
}

export interface PlayFieldSummaryDto {
  id: string;
  name: string;
  ownerId: string;
  isPublic: boolean;
  lastUpdatedOn: string;
  centerCoordinates: GpsCoordinateDto | null;
}

/** Local record stored in IndexedDB — extends summary with points and sync state. */
export interface PlayFieldRecord extends PlayFieldSummaryDto {
  points: GpsCoordinateDto[];
  isSynced: boolean;
}

export interface UpsertPlayFieldRequest {
  name: string;
  isPublic: boolean;
  points: GpsCoordinateDto[];
  lastUpdatedOn: string;
}

/** Full playfield DTO returned by GET /playfields/:id. */
export interface PlayFieldDetailDto {
  id: string;
  name: string;
  ownerId: string;
  isPublic: boolean;
  points: GpsCoordinateDto[];
  lastUpdatedOn: string;
  centerCoordinates: GpsCoordinateDto | null;
}

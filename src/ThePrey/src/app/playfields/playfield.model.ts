export interface GpsCoordinateDto {
  latitude: number;
  longitude: number;
}

/**
 * A playfield may only be made public when its name follows the
 * `CC, City, Fieldname` convention so public listings stay searchable and tidy:
 *  - CC        — a 2–4 letter uppercase country code (e.g. NL, USA)
 *  - City      — starts with a capital; ordinary city-name characters only
 *  - Fieldname — ordinary characters; ampersand (&) and dashes (-) allowed
 *
 * e.g. `NL, Amsterdam, Vondelpark Arena`
 */
const PUBLIC_NAME_PATTERN =
  /^[A-Z]{2,4}, \p{Lu}[\p{L} '’.-]*, [\p{L}\p{N}][\p{L}\p{N} &'’.-]*$/u;

/** True when the name qualifies the playfield to be made public. */
export function isPublicEligibleName(name: string): boolean {
  return PUBLIC_NAME_PATTERN.test(name.trim());
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

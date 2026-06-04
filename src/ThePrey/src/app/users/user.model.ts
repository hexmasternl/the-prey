/** Cached user identity — the 3 fields needed to gate and personalise the home screen. */
export interface UserProfile {
  userId: string;
  callsign: string;
  preferredLanguage: string;
}

/** Matches the server's UserDto (camelCase JSON serialisation). */
export interface UserDto {
  userId: string;
  displayName: string;
  callsign: string;
  emailAddress: string;
  preferredLanguage: string;
}

/** Matches the server's CreateUserRequest. */
export interface CreateUserRequest {
  firstName?: string;
  lastName?: string;
  emailAddress: string;
  isEmailVerified: boolean;
  preferredLanguage?: string;
}

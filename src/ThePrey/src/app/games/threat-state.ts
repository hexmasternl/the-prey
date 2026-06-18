/**
 * Shared threat-state escalation helper used by both the hunter and prey gameplay pages.
 *
 * Three levels:
 *   normal   — green (--tp-signal): standard gameplay
 *   final    — amber (--tp-caution): game has entered its final/endgame stage
 *   critical — red   (--tp-hunter): ≤60 seconds remaining on the clock
 *
 * NOTE (prey): proximity-driven escalation (e.g. hunter is very close) is intentionally
 * absent here. The prey client does NOT receive real-time hunter distance data; the status
 * poll only carries secondsRemaining and isEndgame. Proximity-based prey escalation requires
 * a dedicated backend push of hunter distance to the prey — deferred to a future feature.
 */

export type ThreatState = 'normal' | 'final' | 'critical';

/**
 * Derives the current threat state from seconds remaining and the endgame flag.
 * Critical (≤60 s) takes precedence over final.
 *
 * @param secondsRemaining  Seconds left in the game, or null when unknown.
 * @param isEndgame         True when the server has set the game into its final stage.
 */
export function computeThreatState(
  secondsRemaining: number | null,
  isEndgame: boolean,
): ThreatState {
  if (secondsRemaining != null && secondsRemaining <= 60) return 'critical';
  if (isEndgame) return 'final';
  return 'normal';
}

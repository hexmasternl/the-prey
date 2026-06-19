/**
 * MAP_COLORS — The Prey Design System v1.0 map palette.
 *
 * Leaflet styles geometry (polygons, markers, blips) with literal color
 * strings and cannot read CSS custom properties, so the design tokens are
 * mirrored here as constants. Use these everywhere Leaflet color is set —
 * `playfield-map`, `playfield-area`, `game-hunter`, `game-prey` — instead of
 * ad-hoc Tailwind-family hexes (#3b82f6, #22c55e, #ff9500…).
 *
 * Values mirror the dark-mode tokens in `theme/variables.scss`.
 */
export const MAP_COLORS = {
  /** Signal green — playfield areas, prey, safe/own geometry (--tp-signal). */
  SIGNAL: '#64ff00',
  /** Threat red — hunter / destructive geometry (--tp-hunter). */
  HUNTER: '#ff2f1f',
  /** Caution amber — warnings, other-prey blips (--tp-caution). */
  CAUTION: '#ffb300',
  /** Tagged / inactive grey — eliminated or out-of-play markers. */
  TAGGED: '#888888',
} as const;

export type MapColor = (typeof MAP_COLORS)[keyof typeof MAP_COLORS];
